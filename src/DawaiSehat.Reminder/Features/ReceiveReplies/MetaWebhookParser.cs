using System.Globalization;
using System.Text.Json;

namespace DawaiSehat.Reminder.Features.ReceiveReplies;

/// <param name="Messages">Messages we understand and will handle.</param>
/// <param name="SkippedTypes">The "type" of every message we ignored (image, button ...). Types are not personal data, so they can be logged.</param>
public sealed record ParsedWebhook(IReadOnlyList<InboundMessage> Messages, IReadOnlyList<string> SkippedTypes);

/// <summary>
/// Reads patient messages out of the JSON Meta posts to /webhook. Pure code: no HTTP, no database.
/// Meta nests messages like this: entry[] -> changes[] -> value -> messages[].
/// A "delivered" or "read" notification has value.statuses[] and no messages[]; those are skipped.
/// </summary>
public static class MetaWebhookParser
{
    /// <exception cref="JsonException">The body is not valid JSON.</exception>
    public static ParsedWebhook Parse(ReadOnlyMemory<byte> body, DateTimeOffset receivedAt)
    {
        using var document = JsonDocument.Parse(body);
        var messages = new List<InboundMessage>();
        var skipped = new List<string>();

        foreach (var entry in Items(document.RootElement, "entry"))
        foreach (var change in Items(entry, "changes"))
        {
            if (!change.TryGetProperty("value", out var value)) continue;

            foreach (var message in Items(value, "messages"))
            {
                var parsed = ParseMessage(message, receivedAt);
                if (parsed is not null) messages.Add(parsed);
                else skipped.Add(GetString(message, "type") ?? "unknown");
            }
        }

        return new ParsedWebhook(messages, skipped);
    }

    // Returns null for message types we do not handle yet (images, buttons ...) and for incomplete messages.
    private static InboundMessage? ParseMessage(JsonElement message, DateTimeOffset receivedAt)
    {
        var id = GetString(message, "id");
        var from = GetString(message, "from");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(from)) return null;

        var sentAt = ParseUnixSeconds(GetString(message, "timestamp"));

        switch (GetString(message, "type"))
        {
            case "text":
                var body = message.TryGetProperty("text", out var text) ? GetString(text, "body") : null;
                return body is null ? null : new InboundMessage(id, from, InboundMessageType.Text, body, null, sentAt, receivedAt);

            case "audio":
                var mediaId = message.TryGetProperty("audio", out var audio) ? GetString(audio, "id") : null;
                return mediaId is null ? null : new InboundMessage(id, from, InboundMessageType.Audio, null, mediaId, sentAt, receivedAt);

            default:
                return null;
        }
    }

    // Meta sends the time as a string of seconds since 1970, e.g. "1700000000".
    private static DateTimeOffset? ParseUnixSeconds(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
        && seconds is >= 0 and <= 253_402_300_799 // year 9999; larger values would make FromUnixTimeSeconds throw
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    // Enumerates an array property; a missing property or a non-array value gives an empty list.
    private static IEnumerable<JsonElement> Items(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var array)
        && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : [];

    private static string? GetString(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
