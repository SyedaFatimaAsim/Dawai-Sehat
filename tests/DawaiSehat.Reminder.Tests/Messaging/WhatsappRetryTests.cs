using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using DawaiSehat.Reminder.Messaging.Interfaces;
using DawaiSehat.Reminder.Messaging.Senders;

namespace DawaiSehat.Reminder.Tests.Messaging;

// Test plan: WA-05. A failed WhatsApp send must not be retried automatically,
// otherwise a message Meta already accepted could reach the patient twice.
public class WhatsappRetryTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    [Fact]
    public async Task SendMessage_ServerError_IsNotRetried()
    {
        var handler = new CountingHandler();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("WhatsApp:UseFake", "false");
            b.UseSetting("WhatsApp:PhoneNumberId", "000000000000000");
            b.UseSetting("WhatsApp:Token", "test-token");
            b.UseSetting("WhatsApp:VerifyToken", "test-verify-token");
            b.UseSetting("WhatsApp:AppSecret", "test-app-secret");
            b.ConfigureServices(s => s.AddHttpClient<IWhatsappSender, MetaWhatsappSender>()
                .ConfigurePrimaryHttpMessageHandler(() => handler));
        });

        var sender = factory.Services.GetRequiredService<IWhatsappSender>();
        var act = () => sender.SendMessageAsync("920000000001", "le li");

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().Be(1);
    }
}
