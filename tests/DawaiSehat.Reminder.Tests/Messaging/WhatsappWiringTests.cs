using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using DawaiSehat.Reminder.Messaging.Interfaces;
using DawaiSehat.Reminder.Messaging.Senders;

namespace DawaiSehat.Reminder.Tests.Messaging;

// Test plan: WA-01, WA-02 (WhatsApp:UseFake switch keeps the app runnable without Meta).
public class WhatsappWiringTests
{
    private static WebApplicationFactory<Program> Create(bool useFake) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("WhatsApp:UseFake", useFake.ToString());
            b.UseSetting("WhatsApp:PhoneNumberId", "000000000000000");
            b.UseSetting("WhatsApp:Token", "test-token");
            b.UseSetting("WhatsApp:VerifyToken", "test-verify-token");
            b.UseSetting("WhatsApp:AppSecret", "test-app-secret");
        });

    [Fact]
    public void Sender_UseFakeTrue_IsFakeWhatsappSender()
    {
        using var factory = Create(useFake: true);

        factory.Services.GetRequiredService<IWhatsappSender>()
            .Should().BeOfType<FakeWhatsappSender>();
    }

    [Fact]
    public void Sender_UseFakeFalse_IsMetaWhatsappSender()
    {
        using var factory = Create(useFake: false);

        factory.Services.GetRequiredService<IWhatsappSender>()
            .Should().BeOfType<MetaWhatsappSender>();
    }
}
