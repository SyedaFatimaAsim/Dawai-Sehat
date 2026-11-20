using System.Collections.Concurrent;

namespace DawaiSehat.Reminder.Features.ReceiveReplies;

/// <summary>
/// Temporary store: messages live in memory and are lost on restart.
/// It is replaced by a PostgreSQL <c>inbound_messages</c> table (unique on whatsapp_message_id) in the next PR.
/// </summary>
public sealed class InMemoryInboundMessageStore : IInboundMessageStore
{
    private readonly ConcurrentDictionary<string, InboundMessage> _messages = new();

    public IReadOnlyCollection<InboundMessage> All => _messages.Values.ToArray();

    public Task<bool> TryAddAsync(InboundMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(_messages.TryAdd(message.WhatsappMessageId, message));
}
