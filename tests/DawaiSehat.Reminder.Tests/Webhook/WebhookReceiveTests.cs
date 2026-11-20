using System.Net;
using System.Text;
using DawaiSehat.Reminder.Features.ReceiveReplies;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DawaiSehat.Reminder.Tests.Webhook;

// Test plan: WH-04 to WH-07, WH-12 to WH-16 (POST /webhook).
public class WebhookReceiveTests : IDisposable
{
    private const string AppSecret = "test-app-secret";
    private readonly InMemoryInboundMessageStore _store = new();
    private readonly WebApplicationFactory<Program> _factory;

    public WebhookReceiveTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("WhatsApp:UseFake", "true");
            b.UseSetting("WhatsApp:AppSecret", AppSecret);
            b.ConfigureServices(s => s.AddSingleton<IInboundMessageStore>(_store));
        });
    }

    public void Dispose() => _factory.Dispose();

    private Task<HttpResponseMessage> Post(string json, string? signature = "valid", string secret = AppSecret)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhook")
        {
            Content = new ByteArrayContent(bytes),
        };
        request.Content.Headers.ContentType = new("application/json");
        if (signature == "valid") signature = WebhookSignature.Compute(bytes, secret);
        if (signature is not null) request.Headers.Add("X-Hub-Signature-256", signature);
        return _factory.CreateClient().SendAsync(request);
    }

    [Fact] // WH-04
    public async Task Post_ValidSignedTextMessage_Returns200AndStoresIt()
    {
        var response = await Post(MetaPayloads.Text("wamid.T1", "le li"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = _store.All.Should().ContainSingle().Subject;
        saved.WhatsappMessageId.Should().Be("wamid.T1");
        saved.From.Should().Be(MetaPayloads.Sender);
        saved.Body.Should().Be("le li");
        saved.Type.Should().Be(InboundMessageType.Text);
    }

    [Fact] // WH-05
    public async Task Post_WrongSignature_Returns401AndStoresNothing()
    {
        var response = await Post(MetaPayloads.Text("wamid.T2", "le li"), secret: "someone-elses-secret");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _store.All.Should().BeEmpty();
    }

    [Fact] // WH-12
    public async Task Post_MissingSignatureHeader_Returns401()
    {
        var response = await Post(MetaPayloads.Text("wamid.T3", "le li"), signature: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _store.All.Should().BeEmpty();
    }

    [Fact] // WH-13: fail closed
    public async Task Post_NoAppSecretConfigured_Returns401EvenWithSomeSignature()
    {
        using var noSecret = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("WhatsApp:UseFake", "true");
            b.UseSetting("WhatsApp:AppSecret", "");
            b.ConfigureServices(s => s.AddSingleton<IInboundMessageStore>(_store));
        });
        var bytes = Encoding.UTF8.GetBytes(MetaPayloads.Text("wamid.T4", "le li"));
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhook") { Content = new ByteArrayContent(bytes) };
        request.Headers.Add("X-Hub-Signature-256", WebhookSignature.Compute(bytes, ""));

        var response = await noSecret.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _store.All.Should().BeEmpty();
    }

    [Fact] // WH-06
    public async Task Post_StatusUpdate_Returns200AndStoresNothing()
    {
        var response = await Post(MetaPayloads.StatusUpdate);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _store.All.Should().BeEmpty();
    }

    [Fact] // WH-07: decision = 200, because a retry of the same broken body would not help
    public async Task Post_SignedButMalformedJson_Returns200AndStoresNothing()
    {
        var response = await Post("{ not json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _store.All.Should().BeEmpty();
    }

    [Fact] // WH-14
    public async Task Post_AudioMessage_StoresMediaId()
    {
        var response = await Post(MetaPayloads.Audio("wamid.V1", "media-42"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = _store.All.Should().ContainSingle().Subject;
        saved.Type.Should().Be(InboundMessageType.Audio);
        saved.MediaId.Should().Be("media-42");
        saved.Body.Should().BeNull();
    }

    [Fact] // WH-15 (in-memory; the database version is WH-08)
    public async Task Post_SameMessageTwice_StoresOnceAndBothReturn200()
    {
        var json = MetaPayloads.Text("wamid.D1", "le li");

        var first = await Post(json);
        var second = await Post(json);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        _store.All.Should().ContainSingle();
    }

    // Sends the body WITHOUT a Content-Length header (like a chunked upload). HttpClient fills the header in
    // by itself, so we talk to the test server directly. An attacker would simply leave the header out.
    private async Task<HttpStatusCode> PostSignedBytes(byte[] bytes, bool withContentLength)
    {
        if (withContentLength)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/webhook") { Content = new ByteArrayContent(bytes) };
            request.Headers.Add("X-Hub-Signature-256", WebhookSignature.Compute(bytes, AppSecret));
            return (await _factory.CreateClient().SendAsync(request)).StatusCode;
        }

        _ = _factory.Server; // make sure the test server is running
        var context = await _factory.Server.SendAsync(c =>
        {
            c.Request.Method = "POST";
            c.Request.Path = "/webhook";
            c.Request.Headers["X-Hub-Signature-256"] = WebhookSignature.Compute(bytes, AppSecret);
            c.Request.Body = new MemoryStream(bytes);
            c.Request.ContentLength.Should().BeNull("this test is about bodies that do not announce their size");
        });
        return (HttpStatusCode)context.Response.StatusCode;
    }

    [Theory] // WH-16 (with and without Content-Length)
    [InlineData(true)]
    [InlineData(false)]
    public async Task Post_BodyLargerThanLimit_Returns413(bool withContentLength)
    {
        var tooBig = new byte[WebhookEndpoints.MaxBodyBytes + 1];
        Array.Fill(tooBig, (byte)'x');

        var status = await PostSignedBytes(tooBig, withContentLength);

        status.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        _store.All.Should().BeEmpty();
    }

    [Theory] // WH-18: the limit is inclusive; this is not valid JSON, but valid size, so 200
    [InlineData(true)]
    [InlineData(false)]
    public async Task Post_BodyExactlyAtLimit_IsAccepted(bool withContentLength)
    {
        var atLimit = new byte[WebhookEndpoints.MaxBodyBytes];
        Array.Fill(atLimit, (byte)'x');

        var status = await PostSignedBytes(atLimit, withContentLength);

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact] // WH-17: Urdu text must reach the store unchanged (signature is over the raw bytes)
    public async Task Post_UrduText_IsStoredUnchanged()
    {
        var response = await Post(MetaPayloads.Text("wamid.U1", "لے لی"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _store.All.Should().ContainSingle().Which.Body.Should().Be("لے لی");
    }

    [Fact] // WH-19: Meta's send time is kept, so a late retry is not mistaken for a reply made now
    public async Task Post_StoresMetaTimestampAsSentAt()
    {
        await Post(MetaPayloads.Text("wamid.S1", "le li"));

        _store.All.Should().ContainSingle().Which.SentAt
            .Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000));
    }
}
