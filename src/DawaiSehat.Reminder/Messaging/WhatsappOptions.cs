namespace DawaiSehat.Reminder.Messaging;

public class WhatsappOptions
{
    public bool UseFake { get; set; } = true;
    public string Token { get; set; } = "";
    public string PhoneNumberId { get; set; } = "";
    public string ApiVersion { get; set; } = "v25.0";
    public string VerifyToken { get; set; } = "";
    /// <summary>Meta App Secret, used to check the webhook signature. Set with dotnet user-secrets, never in a file.</summary>
    public string AppSecret { get; set; } = "";
    /// <summary>Must equal the language code of the templates in WhatsApp Manager.</summary>
    public string TemplateLanguage { get; set; } = "en";
}
