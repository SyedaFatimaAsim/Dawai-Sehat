namespace DawaiSehat.Reminder.Messaging;

/// <summary>
/// The approved WhatsApp template names, exactly as in WhatsApp Manager. They are constants, not
/// configuration: an approved name never changes, and one typo here is caught by one test
/// (WhatsappTemplatesTests). If Meta needs a new name, change it here and update the test.
/// </summary>
public static class TemplateNames
{
    public const string Welcome = "welcome_consent";
    public const string DoseReminder = "dose_reminder";
    public const string MissedDose = "caregiver_missed_dose";
    public const string UnwellAlert = "caregiver_unwell_alert";
}
