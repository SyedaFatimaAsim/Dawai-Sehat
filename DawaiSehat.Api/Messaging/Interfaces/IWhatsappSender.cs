namespace DawaiSehat.Api.Messaging.Interfaces
{
    public interface IWhatsappSender
    {
        public Task SendMessageAsync(string phoneNumber, string message);
    }
}
