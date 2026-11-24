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
    public WhatsappTemplateNames Templates { get; set; } = new();
}

/// <summary>Template names exactly as in WhatsApp Manager. Override with WhatsApp__Templates__DoseReminder etc.</summary>
public class WhatsappTemplateNames
{
    public string Welcome { get; set; } = "welcome_consent";
    // "remainder" is a typo, but it is the real name in Meta and approved names cannot be renamed.
    public string DoseReminder { get; set; } = "dose_remainder";
    public string MissedDose { get; set; } = "caregiver_missed_dose";
    public string UnwellAlert { get; set; } = "caregiver_unwell_alert";
}
