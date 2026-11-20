using System.Net;
using System.Text;
using DawaiSehat.Reminder.Features.ReceiveReplies;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
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

    [Fact] // WH-16
    public async Task Post_BodyLargerThanLimit_Returns413()
    {
        var response = await Post(new string('x', 300 * 1024));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        _store.All.Should().BeEmpty();
    }
}
