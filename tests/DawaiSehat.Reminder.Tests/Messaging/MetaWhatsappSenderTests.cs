using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using DawaiSehat.Reminder.Messaging.Senders;

namespace DawaiSehat.Reminder.Tests.Messaging;

// Test plan: WA-03, WA-04 (MetaWhatsappSender against a fake HTTP handler; Meta is never called).
public class MetaWhatsappSenderTests
{
    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }

    private static MetaWhatsappSender Create(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v25.0/123/") },
            NullLogger<MetaWhatsappSender>.Instance);

    [Fact]
    public async Task SendMessage_Success_PostsTextMessageToMessagesEndpoint()
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        await Create(handler).SendMessageAsync("920000000001", "le li");

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.AbsolutePath.Should().EndWith("/messages");
        using var json = JsonDocument.Parse(handler.Body!);
        json.RootElement.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        json.RootElement.GetProperty("to").GetString().Should().Be("920000000001");
        json.RootElement.GetProperty("type").GetString().Should().Be("text");
        json.RootElement.GetProperty("text").GetProperty("body").GetString().Should().Be("le li");
    }

    [Fact]
    public async Task SendMessage_MetaReturnsError_Throws()
    {
        var act = () => Create(new StubHandler(HttpStatusCode.BadRequest))
            .SendMessageAsync("920000000001", "le li");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
