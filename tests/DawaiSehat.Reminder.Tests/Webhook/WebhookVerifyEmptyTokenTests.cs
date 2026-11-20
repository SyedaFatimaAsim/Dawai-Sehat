using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DawaiSehat.Reminder.Tests.Webhook;

// Test plan: WH-11. If no verify token is configured, an empty token in the URL must not pass.
public class WebhookVerifyEmptyTokenTests
{
    [Fact]
    public async Task Verify_NoTokenConfiguredAndEmptyTokenSent_Returns403()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("WhatsApp:UseFake", "true");
            b.UseSetting("WhatsApp:VerifyToken", "");
        });

        var response = await factory.CreateClient().GetAsync(
            "/webhook?hub.mode=subscribe&hub.verify_token=&hub.challenge=12345");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
