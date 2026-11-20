using System.Text.Json;
using DawaiSehat.Reminder.Messaging;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DawaiSehat.Reminder.Features.ReceiveReplies;

public static class WebhookEndpoints
{
    // Meta's payloads are a few hundred bytes. Anything bigger is not from Meta; do not read it all into memory.
    public const int MaxBodyBytes = 256 * 1024;

    public static IEndpointRouteBuilder MapWebhook(this IEndpointRouteBuilder app)
    {
        app.MapGet("/webhook", Verify);
        app.MapPost("/webhook", Receive);
        return app;
    }

    // Meta calls this once, when you register the webhook URL, to check that the URL is really yours.
    private static IResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        IOptions<WhatsappOptions> options,
        ILogger<Program> logger)
    {
        var expected = options.Value.VerifyToken;

        // An empty configured token must never match an empty query value.
        if (mode == "subscribe" && !string.IsNullOrEmpty(expected) && token == expected)
        {
            logger.LogInformation("Webhook verified by Meta");
            return Results.Text(challenge);
        }

        logger.LogWarning("Webhook verification failed");
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // Meta posts every patient message (and every delivery/read notification) here.
    // Rules: check the signature first, answer 200 fast, never fail on a duplicate.
    private static async Task<IResult> Receive(
        HttpRequest request,
        [FromHeader(Name = "X-Hub-Signature-256")] string? signature,
        IOptions<WhatsappOptions> options,
        IInboundMessageStore store,
        TimeProvider time,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Cheap checks first, before reading any body.
        if (string.IsNullOrEmpty(options.Value.AppSecret))
        {
            // Separate message from "bad signature" so a missing setting is not mistaken for an attack.
            logger.LogError("Webhook rejected: WhatsApp:AppSecret is not configured");
            return Results.Unauthorized();
        }

        if (string.IsNullOrEmpty(signature))
        {
            logger.LogWarning("Webhook rejected: no signature header");
            return Results.Unauthorized();
        }

        // Second wall behind our own limit below (Kestrel's default is about 30 MB).
        var sizeLimit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeLimit is { IsReadOnly: false }) sizeLimit.MaxRequestBodySize = MaxBodyBytes;

        var body = await ReadBodyAsync(request, cancellationToken);
        if (body is null)
        {
            logger.LogWarning("Webhook body rejected: larger than {Max} bytes", MaxBodyBytes);
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        if (!WebhookSignature.IsValid(body, signature, options.Value.AppSecret))
        {
            logger.LogWarning("Webhook rejected: invalid signature");
            return Results.Unauthorized();
        }

        ParsedWebhook parsed;
        try
        {
            parsed = MetaWebhookParser.Parse(body, time.GetUtcNow());
        }
        catch (JsonException)
        {
            // Signed by Meta but unreadable: a retry would not help, so answer 200 and do not log the body.
            logger.LogError("Webhook body is not valid JSON");
            return Results.Ok();
        }

        var stored = 0;
        foreach (var message in parsed.Messages)
        {
            if (await store.TryAddAsync(message, cancellationToken)) stored++;
        }

        // Counts and message types only: no phone numbers and no message text in logs.
        logger.LogInformation("Webhook: {Stored} new message(s), {Duplicates} duplicate(s)",
            stored, parsed.Messages.Count - stored);
        if (parsed.SkippedTypes.Count > 0)
        {
            logger.LogWarning("Webhook: skipped {Count} unsupported message(s) of type(s) {Types}",
                parsed.SkippedTypes.Count, string.Join(",", parsed.SkippedTypes.Distinct()));
        }
        return Results.Ok();
    }

    // Reads the body in chunks, so a body with no Content-Length (chunked) is capped too.
    // Returns null when the body is larger than MaxBodyBytes.
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes) return null;

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        try
        {
            while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaxBodyBytes) return null;
                buffer.Write(chunk, 0, read);
            }
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return null; // Kestrel's own limit tripped
        }
        return buffer.ToArray();
    }
}
