using DawaiSehat.Reminder.Features.ReceiveReplies;
using DawaiSehat.Reminder.Messaging;
using DawaiSehat.Reminder.Messaging.Interfaces;
using DawaiSehat.Reminder.Messaging.Senders;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
//builder.Services.AddSingleton<IWhatsappSender, FakeWhatsappSender>();
builder.Services.Configure<WhatsappOptions>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IInboundMessageStore, InMemoryInboundMessageStore>(); // replaced by PostgreSQL in the next PR

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

app.MapPost("/send-message", async (IWhatsappSender whatsappSender, string phoneNumber, string medicine) =>
{
    var text = $"Assalam o Alaikum! {medicine} lene ka waqt ho gaya hai. Lene ke baad 'le li' likh dein.";
    await whatsappSender.SendMessageAsync(phoneNumber, text);
    return Results.Ok("Reminder sent");
});

app.MapWebhook();

app.Run();



// Makes Program visible to WebApplicationFactory in DawaiSehat.Reminder.Tests.
public partial class Program { }
