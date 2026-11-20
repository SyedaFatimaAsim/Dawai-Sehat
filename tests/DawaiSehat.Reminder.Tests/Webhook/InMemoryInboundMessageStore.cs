using System.Collections.Concurrent;
using DawaiSehat.Reminder.Features.ReceiveReplies;

namespace DawaiSehat.Reminder.Tests.Webhook;

/// <summary>Test fake for the inbound message store: no database, easy to inspect.</summary>
public sealed class InMemoryInboundMessageStore : IInboundMessageStore
{
    private readonly ConcurrentDictionary<string, InboundMessage> _messages = new();

    public IReadOnlyCollection<InboundMessage> All => _messages.Values.ToArray();

    public Task<bool> TryAddAsync(InboundMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(_messages.TryAdd(message.WhatsappMessageId, message));
}
