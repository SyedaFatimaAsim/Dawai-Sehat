using FluentAssertions;
using DawaiSehat.Reminder.Messaging;
using DawaiSehat.Reminder.Messaging.Senders;

namespace DawaiSehat.Reminder.Tests.Messaging;

// Test plan: WA-18, WA-19 (the fake sender must not log private data either).
public class FakeWhatsappSenderTests
{
    [Fact]
    public async Task SendTemplate_LogsNameAndCountOnly()
    {
        var logger = new CapturingLogger<FakeWhatsappSender>();

        await new FakeWhatsappSender(logger).SendTemplateAsync("920000000001",
            new TemplateMessage(TemplateNames.DoseReminder, "en", ["Ammi Jaan", "Panadol 500mg"]));

        logger.AllText.Should().Contain(TemplateNames.DoseReminder).And.Contain("2");
        logger.AllText.Should().NotContain("Ammi Jaan").And.NotContain("Panadol").And.NotContain("920000000001");
    }

    [Fact]
    public async Task SendMessage_DoesNotLogNumberOrText()
    {
        var logger = new CapturingLogger<FakeWhatsappSender>();

        await new FakeWhatsappSender(logger).SendMessageAsync("920000000001", "Panadol lene ka waqt");

        logger.AllText.Should().NotContain("920000000001").And.NotContain("Panadol");
    }
}
