using System.Text.Json;

namespace DawaiSehat.Reminder.Features.ReceiveReplies;

/// <summary>
/// Reads patient messages out of the JSON Meta posts to /webhook. Pure code: no HTTP, no database.
/// Meta nests messages like this: entry[] -> changes[] -> value -> messages[].
/// A "delivered" or "read" notification has value.statuses[] and no messages[]; those are skipped.
/// </summary>
public static class MetaWebhookParser
{
    /// <exception cref="JsonException">The body is not valid JSON.</exception>
    public static IReadOnlyList<InboundMessage> Parse(ReadOnlyMemory<byte> body, DateTimeOffset receivedAt)
    {
        using var document = JsonDocument.Parse(body);
        var messages = new List<InboundMessage>();

        foreach (var entry in Items(document.RootElement, "entry"))
        foreach (var change in Items(entry, "changes"))
        {
            if (!change.TryGetProperty("value", out var value)) continue;

            foreach (var message in Items(value, "messages"))
            {
                var parsed = ParseMessage(message, receivedAt);
                if (parsed is not null) messages.Add(parsed);
            }
        }

        return messages;
    }

    // Returns null for message types we do not handle yet (images, buttons ...) and for incomplete messages.
    private static InboundMessage? ParseMessage(JsonElement message, DateTimeOffset receivedAt)
    {
        var id = GetString(message, "id");
        var from = GetString(message, "from");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(from)) return null;

        switch (GetString(message, "type"))
        {
            case "text":
                var body = message.TryGetProperty("text", out var text) ? GetString(text, "body") : null;
                return body is null ? null : new InboundMessage(id, from, InboundMessageType.Text, body, null, receivedAt);

            case "audio":
                var mediaId = message.TryGetProperty("audio", out var audio) ? GetString(audio, "id") : null;
                return mediaId is null ? null : new InboundMessage(id, from, InboundMessageType.Audio, null, mediaId, receivedAt);

            default:
                return null;
        }
    }

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
