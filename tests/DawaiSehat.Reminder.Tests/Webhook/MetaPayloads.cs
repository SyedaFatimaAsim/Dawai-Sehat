namespace DawaiSehat.Reminder.Tests.Webhook;

/// <summary>Sample payloads in the shape Meta posts. Numbers are placeholders, never real.</summary>
internal static class MetaPayloads
{
    public const string Sender = "920000000001";

    public static string Text(string id, string body, string from = Sender) =>
        Wrap($$"""{ "from": "{{from}}", "id": "{{id}}", "timestamp": "1700000000", "type": "text", "text": { "body": "{{body}}" } }""");

    public static string Audio(string id, string mediaId, string from = Sender) =>
        Wrap($$"""{ "from": "{{from}}", "id": "{{id}}", "timestamp": "1700000000", "type": "audio", "audio": { "id": "{{mediaId}}", "mime_type": "audio/ogg; codecs=opus" } }""");

    public static string Image(string id, string from = Sender) =>
        Wrap($$"""{ "from": "{{from}}", "id": "{{id}}", "timestamp": "1700000000", "type": "image", "image": { "id": "media-1" } }""");

    /// <summary>A "delivered" notification: has statuses[] and no messages[].</summary>
    public const string StatusUpdate = """
        { "object": "whatsapp_business_account", "entry": [ { "changes": [ { "field": "messages", "value": {
            "statuses": [ { "id": "wamid.STATUS1", "status": "delivered", "recipient_id": "920000000001" } ] } } ] } ] }
        """;

    private static string Wrap(string message) => $$"""
        { "object": "whatsapp_business_account", "entry": [ { "id": "WABA-ID", "changes": [ { "field": "messages", "value": {
            "messaging_product": "whatsapp", "messages": [ {{message}} ] } } ] } ] }
        """;
}
