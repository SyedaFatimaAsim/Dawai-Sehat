namespace DawaiSehat.Reminder.Messaging.Interfaces
{
    public interface IWhatsappSender
    {
        public Task SendMessageAsync(string phoneNumber, string message);

        /// <summary>Sends an approved template. Needed for anything outside the 24 h window.</summary>
        public Task SendTemplateAsync(string phoneNumber, TemplateMessage template);
    }
}
