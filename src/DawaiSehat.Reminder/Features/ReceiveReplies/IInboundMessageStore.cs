namespace DawaiSehat.Reminder.Features.ReceiveReplies;

public interface IInboundMessageStore
{
    /// <summary>Saves the message. Returns false, saving nothing, if this WhatsApp message id was already stored.</summary>
    Task<bool> TryAddAsync(InboundMessage message, CancellationToken cancellationToken);
}
