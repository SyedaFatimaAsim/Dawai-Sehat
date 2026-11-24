using FluentAssertions;
using Microsoft.Extensions.Options;
using DawaiSehat.Reminder.Messaging;

namespace DawaiSehat.Reminder.Tests.Messaging;

// Test plan: WA-10, WA-11 (template factory and parameter rules).
public class WhatsappTemplatesTests
{
    private static WhatsappTemplates Create(WhatsappOptions? options = null) =>
        new(Options.Create(options ?? new WhatsappOptions()));

    [Fact]
    public void Welcome_HasNameLanguageAndOneParameter()
    {
        var t = Create().Welcome("Ammi");

        t.Name.Should().Be("welcome_consent");
        t.Language.Should().Be("en");
        t.BodyParameters.Should().Equal("Ammi");
    }

    [Fact]
    public void DoseReminder_UsesTheRealMetaNameWithTypo()
    {
        var t = Create().DoseReminder("Ammi", "Panadol 500mg, Disprin");

        t.Name.Should().Be("dose_remainder");
        t.BodyParameters.Should().Equal("Ammi", "Panadol 500mg, Disprin");
    }

    [Fact]
    public void MissedDose_HasFourParametersInOrder()
    {
        var t = Create().MissedDose("Bilal", "Ammi", "Panadol 500mg", "08:00");

        t.Name.Should().Be("caregiver_missed_dose");
        t.BodyParameters.Should().Equal("Bilal", "Ammi", "Panadol 500mg", "08:00");
    }

    [Fact]
    public void UnwellAlert_HasTwoParametersInOrder()
    {
        var t = Create().UnwellAlert("Bilal", "Ammi");

        t.Name.Should().Be("caregiver_unwell_alert");
        t.BodyParameters.Should().Equal("Bilal", "Ammi");
    }

    [Fact]
    public void Names_And_Language_CanBeOverriddenByConfig()
    {
        var options = new WhatsappOptions { TemplateLanguage = "ur" };
        options.Templates.DoseReminder = "dose_reminder_v2";

        var t = Create(options).DoseReminder("Ammi", "Panadol");

        t.Name.Should().Be("dose_reminder_v2");
        t.Language.Should().Be("ur");
    }

    [Theory]
    [InlineData("Panadol\nDisprin", "Panadol Disprin")]
    [InlineData("Panadol\tDisprin", "Panadol Disprin")]
    [InlineData("Panadol    Disprin", "Panadol Disprin")]
    [InlineData("  Panadol  ", "Panadol")]
    public void Parameters_AreSanitisedToSingleSpaces(string input, string expected)
    {
        Create().Welcome(input).BodyParameters.Should().Equal(expected);
    }

    // Test plan: WA-17
    [Fact]
    public void Parameter_AtMaxLength_IsAccepted_AndOverMaxThrows()
    {
        Create().Welcome(new string('a', 1024)).BodyParameters[0].Should().HaveLength(1024);

        var act = () => Create().Welcome(new string('a', 1025));

        act.Should().Throw<ArgumentException>().WithMessage("*1024*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public void EmptyParameter_Throws(string input)
    {
        var act = () => Create().DoseReminder("Ammi", input);

        act.Should().Throw<ArgumentException>();
    }
}
