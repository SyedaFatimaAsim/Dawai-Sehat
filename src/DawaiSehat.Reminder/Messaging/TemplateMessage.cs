namespace DawaiSehat.Reminder.Messaging;

/// <summary>
/// A WhatsApp template (pre-approved message). Outside the 24 h window Meta only accepts templates.
/// Name and Language must match WhatsApp Manager exactly. BodyParameters fill {{1}}, {{2}}, ... in order.
/// </summary>
public record TemplateMessage(string Name, string Language, IReadOnlyList<string> BodyParameters);
