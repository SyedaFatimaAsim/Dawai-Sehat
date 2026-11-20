namespace DawaiSehat.Reminder.Data;

/// <summary>One row of the inbound_messages table: a message a patient sent us on WhatsApp.</summary>
public class InboundMessageRow
{
    public Guid Id { get; set; }

    /// <summary>Meta's message id ("wamid..."). UNIQUE: the database itself refuses a duplicate delivery.</summary>
    public string WhatsappMessageId { get; set; } = "";

    public string FromNumber { get; set; } = "";

    /// <summary>"text" or "audio".</summary>
    public string Type { get; set; } = "";

    public string? Body { get; set; }
    public string? MediaId { get; set; }

    /// <summary>When the patient sent it, from Meta's timestamp. Null if Meta sent none.</summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>When we received it. Always UTC (timestamptz).</summary>
    public DateTimeOffset ReceivedAt { get; set; }
}
