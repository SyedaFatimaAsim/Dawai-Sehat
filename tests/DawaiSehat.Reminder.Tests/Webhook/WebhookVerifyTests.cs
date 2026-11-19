using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DawaiSehat.Reminder.Tests.Webhook;

// Test plan: WH-01, WH-02, WH-03 (GET /webhook, Meta verification handshake).
public class WebhookVerifyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string VerifyToken = "test-verify-token";
    private readonly HttpClient _client;

    public WebhookVerifyTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("WhatsApp:UseFake", "true");
            b.UseSetting("WhatsApp:VerifyToken", VerifyToken);
        }).CreateClient();
    }

    [Fact]
    public async Task Verify_CorrectToken_ReturnsChallenge()
    {
        var response = await _client.GetAsync(
            $"/webhook?hub.mode=subscribe&hub.verify_token={VerifyToken}&hub.challenge=12345");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("12345");
    }

    [Fact]
    public async Task Verify_WrongToken_Returns403()
    {
        var response = await _client.GetAsync(
            "/webhook?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=12345");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Verify_MissingParameters_Returns403()
    {
        var response = await _client.GetAsync("/webhook");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
