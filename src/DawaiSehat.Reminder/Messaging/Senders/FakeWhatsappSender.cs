using DawaiSehat.Reminder.Messaging;
using DawaiSehat.Reminder.Messaging.Interfaces;

namespace DawaiSehat.Reminder.Messaging.Senders
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
            // Length only: the number and text are private data.
            _logger.LogInformation("FakeWhatsappSender: message of {Length} chars", message.Length);
            return Task.CompletedTask;
        }

        public Task SendTemplateAsync(string phoneNumber, TemplateMessage template)
        {
            // Name and count only: parameters contain patient names and medicines.
            _logger.LogInformation("FakeWhatsappSender: template {Template} with {Count} parameters",
                template.Name, template.BodyParameters.Count);
            return Task.CompletedTask;
        }

    }
}
