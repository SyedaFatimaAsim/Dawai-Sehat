using DawaiSehat.Reminder.Data;
using DawaiSehat.Reminder.Features.ReceiveReplies;
using DawaiSehat.Reminder.Messaging;
using DawaiSehat.Reminder.Messaging.Interfaces;
using DawaiSehat.Reminder.Messaging.Senders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
//builder.Services.AddSingleton<IWhatsappSender, FakeWhatsappSender>();
// ValidateOnStart: with UseFake=false the app refuses to start if a Meta setting is missing,
// instead of failing later with a puzzling 401 or 400 while a patient is waiting for a reminder.
builder.Services.AddOptions<WhatsappOptions>()
    .Bind(builder.Configuration.GetSection("WhatsApp"))
    .Validate(o => o.UseFake
            || (!string.IsNullOrWhiteSpace(o.Token) && !string.IsNullOrWhiteSpace(o.PhoneNumberId)
                && !string.IsNullOrWhiteSpace(o.VerifyToken) && !string.IsNullOrWhiteSpace(o.AppSecret)),
        "When WhatsApp:UseFake is false, WhatsApp:Token, WhatsApp:PhoneNumberId, WhatsApp:VerifyToken and WhatsApp:AppSecret must all be set (use dotnet user-secrets).")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WhatsappTemplates>();
// Each service has its own database (reminder_db). Aspire injects ConnectionStrings:reminder-db;
// when running without Aspire, set it with dotnet user-secrets (it contains a password).
builder.Services.AddDbContext<ReminderDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("reminder-db")
        ?? throw new InvalidOperationException("ConnectionStrings:reminder-db is not set. Run through the AppHost, or use dotnet user-secrets.")));
builder.Services.AddScoped<IInboundMessageStore, PostgresInboundMessageStore>();

var useFake = builder.Configuration.GetValue("WhatsApp:UseFake", true);

if (useFake)
    builder.Services.AddSingleton<IWhatsappSender, FakeWhatsappSender>();
else
{
    builder.Services.AddHttpClient<IWhatsappSender, MetaWhatsappSender>((sp, client) =>
    {
        var opt = sp.GetRequiredService<IOptions<WhatsappOptions>>().Value;
        client.BaseAddress = new Uri($"https://graph.facebook.com/{opt.ApiVersion}/{opt.PhoneNumberId}/");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opt.Token);
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints(); // /health and /alive (Development only, see ServiceDefaults)

app.UseHttpsRedirection();

app.MapGet("/", () => "Dawai Sehat is running");

// Manual-testing endpoints, unauthenticated: never expose them with real Meta in production.
// Remove or protect both once the scheduler exists.
if (app.Environment.IsDevelopment() || useFake)
{
    app.MapPost("/send-message", async (IWhatsappSender whatsappSender, string phoneNumber, string medicine) =>
    {
        var text = $"Assalam o Alaikum! {medicine} lene ka waqt ho gaya hai. Lene ke baad 'le li' likh dein.";
        await whatsappSender.SendMessageAsync(phoneNumber, text);
        return Results.Ok("Reminder sent");
    });

    app.MapPost("/send-template/dose-reminder", async (IWhatsappSender whatsappSender, WhatsappTemplates templates,
        string phoneNumber, string patientName, string medicines) =>
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(phoneNumber, @"^92\d{10}$"))
            return Results.BadRequest("phoneNumber must be 92 followed by 10 digits.");

        TemplateMessage template;
        try
        {
            template = templates.DoseReminder(patientName, medicines);
        }
        catch (ArgumentException ex)
        {
            // Empty or too long patientName/medicines: short message only, no stack trace.
            return Results.BadRequest(ex.Message);
        }

        await whatsappSender.SendTemplateAsync(phoneNumber, template);
        return Results.Ok("Template sent");
    });
}

app.MapWebhook();

var hasDatabase = !string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("reminder-db"));
var migrateOnly = args.Contains("--migrate");

// --migrate is a one-shot deployment step. Without a database it must fail loudly, never fall through
// to starting the web server (a deploy script would hang waiting for it to exit).
if (!hasDatabase && (migrateOnly || !app.Environment.IsDevelopment()))
{
    throw new InvalidOperationException(
        "ConnectionStrings:reminder-db must be set (always for --migrate, and outside Development).");
}

if (!hasDatabase)
{
    app.Logger.LogWarning("ConnectionStrings:reminder-db is not set: saving webhook messages will fail. Run through the AppHost.");
}

// Migrations: `dotnet run -- --migrate` applies them and exits (the separate deployment step).
// In Development they are also applied at startup, so a fresh database just works.
// If the service starts in production BEFORE --migrate has run, webhook calls return 500 ("relation does not exist")
// and Meta retries; nothing is lost. See docs/deployment-handoff.md.
if (hasDatabase && (migrateOnly || app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ReminderDbContext>().Database.MigrateAsync();
    if (migrateOnly) return;
}

app.Run();



// Makes Program visible to WebApplicationFactory in DawaiSehat.Reminder.Tests.
public partial class Program { }
