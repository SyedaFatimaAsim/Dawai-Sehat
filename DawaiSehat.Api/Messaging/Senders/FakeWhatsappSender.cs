using DawaiSehat.Api.Messaging.Interfaces;

namespace DawaiSehat.Api.Messaging.Senders
{
    public class FakeWhatsappSender:IWhatsappSender
    {
        private readonly ILogger<FakeWhatsappSender> _logger;
        public FakeWhatsappSender(ILogger<FakeWhatsappSender> logger)
        {
            _logger = logger;
        }

        public Task SendMessageAsync(string phoneNumber, string message)
        {
            _logger.LogInformation($"FakeWhatsappSender: Sending message to {phoneNumber}: {message}");
            return Task.CompletedTask;
        }

    }
}
