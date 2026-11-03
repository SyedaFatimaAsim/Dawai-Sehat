using DawaiSehat.Api.Messaging.DawaiSehat.Api.Messaging;
using DawaiSehat.Api.Messaging.Interfaces;
using DawaiSehat.Api.Messaging.Senders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
//builder.Services.AddSingleton<IWhatsappSender, FakeWhatsappSender>();
builder.Services.Configure<WhatsappOptions>(builder.Configuration.GetSection("WhatsApp"));

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

app.UseHttpsRedirection();

app.MapGet("/", () => "Dawai Sehat is running");

app.MapPost("/send-message", async (IWhatsappSender whatsappSender, string phoneNumber, string medicine) =>
{
    var text = $"Assalam o Alaikum! {medicine} lene ka waqt ho gaya hai. Lene ke baad 'le li' likh dein.";
    await whatsappSender.SendMessageAsync(phoneNumber, text);
    return Results.Ok("Reminder sent");
});

app.MapGet("/webhook", (
    [FromQuery(Name = "hub.mode")] string? mode,
    [FromQuery(Name = "hub.verify_token")] string? token,
    [FromQuery(Name = "hub.challenge")] string? challenge,
    IOptions<WhatsappOptions> options,
    ILogger<Program> logger) =>
{
    if (mode == "subscribe" && token == options.Value.VerifyToken)
    {
        logger.LogInformation("Webhook verified by Meta");
        return Results.Text(challenge);
    }

    logger.LogWarning("Webhook verification failed");
    return Results.StatusCode(403);
});


app.MapPost("/webhook", async (HttpRequest request, ILogger<Program> logger) =>
{
    using var reader = new StreamReader(request.Body);
    var json = await reader.ReadToEndAsync();

    logger.LogInformation("Webhook received: {Json}", json);

    return Results.Ok();
});

app.Run();


