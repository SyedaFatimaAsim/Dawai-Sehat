namespace DawaiSehat.Reminder.Features.ReceiveReplies;

public enum InboundMessageType
{
    Text,
    Audio,
}

/// <summary>One message a patient sent us on WhatsApp, after it was read out of Meta's JSON.</summary>
/// <param name="WhatsappMessageId">Meta's unique id (starts with "wamid."). Used to ignore duplicate deliveries.</param>
/// <param name="From">The sender's WhatsApp number, digits only.</param>
/// <param name="Body">The text for a text message; null for audio.</param>
/// <param name="MediaId">Meta's id of the voice note, to download it later; null for text.</param>
/// <param name="ReceivedAt">When WE received it (UTC), not when the patient sent it.</param>
public sealed record InboundMessage(
    string WhatsappMessageId,
    string From,
    InboundMessageType Type,
    string? Body,
    string? MediaId,
    DateTimeOffset ReceivedAt);
