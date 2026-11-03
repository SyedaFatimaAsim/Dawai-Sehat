using DawaiSehat.Api.Messaging.DawaiSehat.Api.Messaging;
using DawaiSehat.Api.Messaging.Interfaces;

namespace DawaiSehat.Api.Messaging.Senders
{
    public class MetaWhatsappSender:IWhatsappSender
    {
        private readonly HttpClient _http;
        private readonly ILogger<MetaWhatsappSender> _logger;

        public MetaWhatsappSender(HttpClient http, ILogger<MetaWhatsappSender> logger)
        {
            _http = http;
            _logger = logger;
        }
       
        public async Task SendMessageAsync(string phoneNumber, string message)
        {
            var body = new
            {
                messaging_product = "whatsapp",
                to = phoneNumber,
                type = "text",
                text = new { body = message }
            };

            var response = await _http.PostAsJsonAsync("messages", body);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("WhatsApp send failed: {Status} {Error}", response.StatusCode, error);
                throw new InvalidOperationException($"WhatsApp send failed: {response.StatusCode}");
            }

            _logger.LogInformation("WhatsApp message sent to {Phone}", phoneNumber);
        }
    }
}
