namespace DawaiSehat.Reminder.Features.ReceiveReplies;

public enum InboundMessageType
{
    Text,
    Audio,
}

/// <summary>One message a patient sent us on WhatsApp, after it was read out of Meta's JSON.</summary>
/// <param name="WhatsappMessageId">Meta's unique id (starts with "wamid."). Used to ignore duplicate deliveries.</param>
/// <param name="From">The sender's WhatsApp number, exactly as Meta sends it.</param>
/// <param name="Body">The text for a text message; null for audio.</param>
/// <param name="MediaId">Meta's id of the voice note, to download it later; null for text.</param>
/// <param name="SentAt">When the patient sent it (UTC), from Meta's timestamp. Null if Meta did not send a valid one.
/// Meta retries failed deliveries for hours, so this can be much earlier than <paramref name="ReceivedAt"/>.
/// A late retry of "le li" must still count as the time the patient actually replied.</param>
/// <param name="ReceivedAt">When WE received it (UTC).</param>
public sealed record InboundMessage(
    string WhatsappMessageId,
    string From,
    InboundMessageType Type,
    string? Body,
    string? MediaId,
    DateTimeOffset? SentAt,
    DateTimeOffset ReceivedAt);
