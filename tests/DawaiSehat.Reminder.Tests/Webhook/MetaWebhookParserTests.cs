using System.Text;
using System.Text.Json;
using DawaiSehat.Reminder.Features.ReceiveReplies;
using FluentAssertions;

namespace DawaiSehat.Reminder.Tests.Webhook;

// Test plan: WP-01 to WP-07 (reading Meta's JSON).
public class MetaWebhookParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<InboundMessage> Parse(string json) =>
        MetaWebhookParser.Parse(Encoding.UTF8.GetBytes(json), Now);

    [Fact] // WP-01
    public void Parse_TextMessage_ReturnsTextMessage()
    {
        var result = Parse(MetaPayloads.Text("wamid.A1", "le li"));

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new InboundMessage(
            "wamid.A1", MetaPayloads.Sender, InboundMessageType.Text, "le li", null, Now));
    }

    [Fact] // WP-02
    public void Parse_AudioMessage_ReturnsAudioWithMediaId()
    {
        var result = Parse(MetaPayloads.Audio("wamid.A2", "media-42"));

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new InboundMessage(
            "wamid.A2", MetaPayloads.Sender, InboundMessageType.Audio, null, "media-42", Now));
    }

    [Fact] // WP-03
    public void Parse_StatusUpdate_ReturnsNothing() =>
        Parse(MetaPayloads.StatusUpdate).Should().BeEmpty();

    [Fact] // WP-04
    public void Parse_UnsupportedMessageType_IsSkipped() =>
        Parse(MetaPayloads.Image("wamid.A3")).Should().BeEmpty();

    [Fact] // WP-05
    public void Parse_TwoMessagesInOnePost_ReturnsBoth()
    {
        const string json = """
            { "entry": [ { "changes": [ { "value": { "messages": [
                { "from": "920000000001", "id": "wamid.B1", "type": "text", "text": { "body": "le li" } },
                { "from": "920000000002", "id": "wamid.B2", "type": "text", "text": { "body": "nahi" } } ] } } ] } ] }
            """;

        Parse(json).Select(m => m.WhatsappMessageId).Should().Equal("wamid.B1", "wamid.B2");
    }

    [Theory] // WP-06: incomplete or oddly shaped JSON never throws
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("""{ "entry": "x" }""")]
    [InlineData("""{ "entry": [ { "changes": [ { } ] } ] }""")]
    [InlineData("""{ "entry": [ { "changes": [ { "value": { "messages": [ { "type": "text" } ] } } ] } ] }""")]
    [InlineData("""{ "entry": [ { "changes": [ { "value": { "messages": [ { "id": "w", "from": "9", "type": "text", "text": {} } ] } } ] } ] }""")]
    public void Parse_IncompleteShapes_ReturnsNothingWithoutThrowing(string json) =>
        Parse(json).Should().BeEmpty();

    [Fact] // WP-07
    public void Parse_InvalidJson_ThrowsJsonException()
    {
        var act = () => Parse("{ not json");

        act.Should().Throw<JsonException>();
    }
}
