using System.Text.Json;
using DawaiSehat.Reminder.Messaging;
using DawaiSehat.Reminder.Messaging.Interfaces;

namespace DawaiSehat.Reminder.Messaging.Senders
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
                // Status only: the Meta error body can echo the recipient or message text.
                _logger.LogError("WhatsApp send failed: {Status}", response.StatusCode);
                throw new InvalidOperationException($"WhatsApp send failed: {response.StatusCode}");
            }

            // No phone number or text in the log (private data), length only.
            _logger.LogInformation("WhatsApp message sent ({Length} chars)", message.Length);
        }

        public async Task SendTemplateAsync(string phoneNumber, TemplateMessage template)
        {
            var body = new
            {
                messaging_product = "whatsapp",
                to = phoneNumber,
                type = "template",
                template = new
                {
                    name = template.Name,
                    language = new { code = template.Language },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = template.BodyParameters.Select(p => new { type = "text", text = p }).ToArray()
                        }
                    }
                }
            };

            var response = await _http.PostAsJsonAsync("messages", body);

            if (!response.IsSuccessStatusCode)
            {
                var (code, type, trace) = await ReadSafeErrorFieldsAsync(response);
                _logger.LogError(
                    "WhatsApp template send failed: {Status} {Template} code={Code} type={Type} fbtrace_id={Trace}",
                    response.StatusCode, template.Name, code, type, trace);
                throw new InvalidOperationException($"WhatsApp template send failed: {response.StatusCode}");
            }

            // No phone number and no parameters in the log (private data).
            _logger.LogInformation("WhatsApp template {Template} sent", template.Name);
        }

        /// <summary>
        /// Reads only error.code, error.type and error.fbtrace_id from Meta's error JSON. The rest of the body
        /// (error.message, error_data) can contain the recipient number or patient data, so it is never logged.
        /// Returns nulls if the body is not the expected JSON.
        /// </summary>
        private static async Task<(string? Code, string? Type, string? Trace)> ReadSafeErrorFieldsAsync(
            HttpResponseMessage response)
        {
            try
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var error = doc.RootElement.GetProperty("error");
                return (Field(error, "code"), Field(error, "type"), Field(error, "fbtrace_id"));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return (null, null, null);
            }
        }

        private static string? Field(JsonElement error, string name) =>
            error.TryGetProperty(name, out var value) ? value.ToString() : null;
    }
}
