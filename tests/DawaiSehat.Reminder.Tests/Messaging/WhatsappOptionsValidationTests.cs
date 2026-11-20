using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DawaiSehat.Reminder.Tests.Messaging;

// Test plan: WA-06, WA-07. A missing Meta setting must stop the app at startup, not fail later per request.
public class WhatsappOptionsValidationTests
{
    private static WebApplicationFactory<Program> Create(bool useFake, string appSecret) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("WhatsApp:UseFake", useFake.ToString());
            b.UseSetting("WhatsApp:Token", "test-token");
            b.UseSetting("WhatsApp:PhoneNumberId", "000000000000000");
            b.UseSetting("WhatsApp:VerifyToken", "test-verify-token");
            b.UseSetting("WhatsApp:AppSecret", appSecret);
        });

    [Fact] // WA-06
    public void Start_RealSenderWithoutAppSecret_FailsAndNamesTheSetting()
    {
        using var factory = Create(useFake: false, appSecret: "");

        var act = () => factory.CreateClient();

        act.Should().Throw<Exception>().Where(e => e.ToString().Contains("WhatsApp:AppSecret"));
    }

    [Fact] // WA-07: local development without Meta must keep working
    public void Start_FakeSenderWithoutAnySecrets_Starts()
    {
        using var factory = Create(useFake: true, appSecret: "");

        var act = () => factory.CreateClient();

        act.Should().NotThrow();
    }
}
