using DawaiSehat.Reminder.Data;
using Microsoft.EntityFrameworkCore;

namespace DawaiSehat.Reminder.Features.ReceiveReplies;

public sealed class PostgresInboundMessageStore(ReminderDbContext db) : IInboundMessageStore
{
    public async Task<bool> TryAddAsync(InboundMessage message, CancellationToken cancellationToken)
    {
        // An unknown type must fail loudly, not be saved as "text".
        var type = message.Type switch
        {
            InboundMessageType.Text => "text",
            InboundMessageType.Audio => "audio",
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.Type, "Unknown message type"),
        };

        // One statement, decided by the database: "insert this row unless the id already exists".
        // Checking first ("does it exist?") and inserting second would race when Meta delivers the same
        // message twice at the same moment. ON CONFLICT cannot race: the unique index settles it.
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO inbound_messages
                (id, whatsapp_message_id, from_number, type, body, media_id, sent_at, received_at)
            VALUES
                ({Guid.CreateVersion7()}, {message.WhatsappMessageId}, {message.From}, {type},
                 {message.Body}, {message.MediaId}, {message.SentAt?.ToUniversalTime()}, {message.ReceivedAt.ToUniversalTime()})
            ON CONFLICT (whatsapp_message_id) DO NOTHING
            """, cancellationToken);

        return inserted == 1; // 0 = duplicate, nothing saved
    }
}
