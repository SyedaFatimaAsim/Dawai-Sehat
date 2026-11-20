namespace DawaiSehat.Reminder.Messaging.Interfaces
{
    public interface IWhatsappSender
    {
        public Task SendMessageAsync(string phoneNumber, string message);
    }
}
