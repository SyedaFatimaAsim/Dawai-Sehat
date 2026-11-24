using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using DawaiSehat.Reminder.Messaging;
using DawaiSehat.Reminder.Messaging.Senders;

namespace DawaiSehat.Reminder.Tests.Messaging;

// Test plan: WA-03, WA-04 (MetaWhatsappSender against a fake HTTP handler; Meta is never called).
public class MetaWhatsappSenderTests
{
    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
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

    // Test plan: WA-08
    [Fact]
    public async Task SendTemplate_Success_PostsTemplatePayload()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var template = new TemplateMessage(TemplateNames.DoseReminder, "en", ["Ammi", "Panadol 500mg, Disprin"]);

        await Create(handler).SendTemplateAsync("920000000001", template);

        handler.Request!.RequestUri!.AbsolutePath.Should().EndWith("/messages");
        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        root.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        root.GetProperty("to").GetString().Should().Be("920000000001");
        root.GetProperty("type").GetString().Should().Be("template");
        var t = root.GetProperty("template");
        t.GetProperty("name").GetString().Should().Be(TemplateNames.DoseReminder);
        t.GetProperty("language").GetProperty("code").GetString().Should().Be("en");
        var component = t.GetProperty("components")[0];
        component.GetProperty("type").GetString().Should().Be("body");
        var texts = component.GetProperty("parameters").EnumerateArray()
            .Select(p => p.GetProperty("text").GetString()).ToArray();
        texts.Should().Equal("Ammi", "Panadol 500mg, Disprin");
        component.GetProperty("parameters")[0].GetProperty("type").GetString().Should().Be("text");
    }

    // Test plan: WA-09
    [Fact]
    public async Task SendTemplate_MetaReturnsError_ThrowsAfterOneCall()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest);

        var act = () => Create(handler).SendTemplateAsync("920000000001",
            new TemplateMessage("missing_template", "en", ["x"]));

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().Be(1);
    }

    private sealed class BodyHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private static MetaWhatsappSender Create(HttpMessageHandler handler, CapturingLogger<MetaWhatsappSender> logger) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v25.0/123/") }, logger);

    // Test plan: WA-14
    [Fact]
    public async Task SendTemplate_MetaError_LogsOnlyCodeTypeAndTraceNotRecipientOrPatient()
    {
        const string metaError = """
            {"error":{"message":"Recipient 920000000001 not valid for Ammi Jaan","type":"OAuthException","code":131026,
             "error_data":{"details":"Ammi Jaan 920000000001"},"fbtrace_id":"TRACE123"}}
            """;
        var logger = new CapturingLogger<MetaWhatsappSender>();

        var act = () => Create(new BodyHandler(HttpStatusCode.BadRequest, metaError), logger)
            .SendTemplateAsync("920000000001", new TemplateMessage(TemplateNames.DoseReminder, "en", ["Ammi Jaan", "Panadol"]));

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.AllText.Should().Contain("131026").And.Contain("OAuthException").And.Contain("TRACE123");
        logger.AllText.Should().NotContain("920000000001").And.NotContain("Ammi Jaan");
    }

    // Test plan: WA-15
    [Fact]
    public async Task SendTemplate_MetaErrorBodyNotJson_LogsStatusOnlyAndStillThrows()
    {
        var logger = new CapturingLogger<MetaWhatsappSender>();

        var act = () => Create(new BodyHandler(HttpStatusCode.BadGateway, "<html>920000000001</html>"), logger)
            .SendTemplateAsync("920000000001", new TemplateMessage(TemplateNames.DoseReminder, "en", ["x"]));

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.AllText.Should().Contain("BadGateway").And.NotContain("920000000001");
    }

    // Test plan: WA-16
    [Fact]
    public async Task SendMessage_LogsDoNotContainNumberOrText()
    {
        var logger = new CapturingLogger<MetaWhatsappSender>();

        await Create(new BodyHandler(HttpStatusCode.OK, "{}"), logger)
            .SendMessageAsync("920000000001", "Panadol lene ka waqt");

        logger.AllText.Should().NotContain("920000000001").And.NotContain("Panadol");
    }

    // Test plan: WA-16
    [Fact]
    public async Task SendMessage_ErrorLogDoesNotContainMetaBody()
    {
        var logger = new CapturingLogger<MetaWhatsappSender>();

        var act = () => Create(new BodyHandler(HttpStatusCode.BadRequest, """{"error":{"message":"920000000001"}}"""), logger)
            .SendMessageAsync("920000000001", "le li");

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.AllText.Should().NotContain("920000000001");
    }

    [Fact]
    public async Task SendMessage_MetaReturnsError_Throws()
    {
        var act = () => Create(new StubHandler(HttpStatusCode.BadRequest))
            .SendMessageAsync("920000000001", "le li");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
