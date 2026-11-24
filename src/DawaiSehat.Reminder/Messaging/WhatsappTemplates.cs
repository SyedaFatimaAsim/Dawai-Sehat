using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace DawaiSehat.Reminder.Messaging;

/// <summary>
/// Builds the four approved templates. One method per template, so the number and order of
/// parameters are fixed in code and a caller cannot get {{1}}, {{2}} mixed up.
/// Names are the constants in <see cref="TemplateNames"/>; the language code comes from configuration.
/// Both must equal WhatsApp Manager exactly.
/// </summary>
public class WhatsappTemplates
{
    internal const int MaxParameterLength = 1024;

    private readonly WhatsappOptions _options;

    public WhatsappTemplates(IOptions<WhatsappOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>{{1}} patient name.</summary>
    public TemplateMessage Welcome(string patientName) =>
        Build(TemplateNames.Welcome, patientName);

    /// <summary>{{1}} patient name, {{2}} medicine list on one line (the caller joins with ", ").</summary>
    public TemplateMessage DoseReminder(string patientName, string medicineList) =>
        Build(TemplateNames.DoseReminder, patientName, medicineList);

    /// <summary>{{1}} caregiver, {{2}} patient, {{3}} medicine + strength, {{4}} time (Asia/Karachi).</summary>
    public TemplateMessage MissedDose(string caregiverName, string patientName, string medicineAndStrength, string time) =>
        Build(TemplateNames.MissedDose, caregiverName, patientName, medicineAndStrength, time);

    /// <summary>{{1}} caregiver, {{2}} patient.</summary>
    public TemplateMessage UnwellAlert(string caregiverName, string patientName) =>
        Build(TemplateNames.UnwellAlert, caregiverName, patientName);

    private TemplateMessage Build(string name, params string[] parameters) =>
        new(name, _options.TemplateLanguage, parameters.Select(Sanitise).ToList());

    /// <summary>
    /// Meta rejects a parameter with a newline, a tab, 4 or more spaces in a row, or an empty value.
    /// Collapsing all whitespace to single spaces avoids a 400 at reminder time.
    /// An empty value is a bug in the caller, so it throws instead of sending a broken message.
    /// A value longer than <see cref="MaxParameterLength"/> (1024) characters also throws; Meta rejects very long parameters.
    /// </summary>
    internal static string Sanitise(string value)
    {
        var cleaned = Regex.Replace(value ?? "", @"\s+", " ").Trim();
        if (cleaned.Length == 0)
            throw new ArgumentException("A WhatsApp template parameter must not be empty.");
        if (cleaned.Length > MaxParameterLength)
            throw new ArgumentException($"A WhatsApp template parameter must not be longer than {MaxParameterLength} characters.");
        return cleaned;
    }
}
