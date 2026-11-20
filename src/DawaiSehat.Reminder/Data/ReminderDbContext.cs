using Microsoft.EntityFrameworkCore;

namespace DawaiSehat.Reminder.Data;

/// <summary>The Reminder service's own database (reminder_db). No other service reads it.</summary>
public class ReminderDbContext(DbContextOptions<ReminderDbContext> options) : DbContext(options)
{
    public DbSet<InboundMessageRow> InboundMessages => Set<InboundMessageRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // snake_case names, as the architecture asks. Written out by hand: the table is small.
        modelBuilder.Entity<InboundMessageRow>(e =>
        {
            e.ToTable("inbound_messages");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.WhatsappMessageId).HasColumnName("whatsapp_message_id").HasMaxLength(200).IsRequired();
            e.Property(x => x.FromNumber).HasColumnName("from_number").HasMaxLength(32).IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(16).IsRequired();
            e.Property(x => x.Body).HasColumnName("body");
            e.Property(x => x.MediaId).HasColumnName("media_id").HasMaxLength(200);
            e.Property(x => x.SentAt).HasColumnName("sent_at");
            e.Property(x => x.ReceivedAt).HasColumnName("received_at");

            // The second wall against duplicate webhook deliveries (architecture.md section 7).
            e.HasIndex(x => x.WhatsappMessageId).IsUnique().HasDatabaseName("ux_inbound_messages_whatsapp_message_id");
        });
    }
}
