namespace DawaiSehat.Reminder.Domain;

/// <summary>What a patient's reply means. Only these five values exist (architecture.md section 8).</summary>
public enum ReplyIntent
{
    /// <summary>We could not tell. The caller asks a plain yes/no question or uses Gemini.</summary>
    Unknown,
    Taken,
    Later,
    NotTaken,
    /// <summary>The patient says they feel unwell. Always alerts the caregiver.</summary>
    Unwell,
}
