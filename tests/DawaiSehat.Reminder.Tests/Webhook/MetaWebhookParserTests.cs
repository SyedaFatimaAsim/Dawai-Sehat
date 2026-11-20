using System.Text;
using System.Text.Json;
using DawaiSehat.Reminder.Features.ReceiveReplies;
using FluentAssertions;

namespace DawaiSehat.Reminder.Tests.Webhook;

// Test plan: WP-01 to WP-09 (reading Meta's JSON).
public class MetaWebhookParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SentAt = DateTimeOffset.FromUnixTimeSeconds(1700000000);

    private static ParsedWebhook Parse(string json) =>
        MetaWebhookParser.Parse(Encoding.UTF8.GetBytes(json), Now);

    [Fact] // WP-01
    public void Parse_TextMessage_ReturnsTextMessage()
    {
        var result = Parse(MetaPayloads.Text("wamid.A1", "le li"));

        result.Messages.Should().ContainSingle().Which.Should().BeEquivalentTo(new InboundMessage(
            "wamid.A1", MetaPayloads.Sender, InboundMessageType.Text, "le li", null, SentAt, Now));
    }

    [Fact] // WP-02
    public void Parse_AudioMessage_ReturnsAudioWithMediaId()
    {
        var result = Parse(MetaPayloads.Audio("wamid.A2", "media-42"));

        result.Messages.Should().ContainSingle().Which.Should().BeEquivalentTo(new InboundMessage(
            "wamid.A2", MetaPayloads.Sender, InboundMessageType.Audio, null, "media-42", SentAt, Now));
    }

    [Fact] // WP-03
    public void Parse_StatusUpdate_ReturnsNothing()
    {
        var result = Parse(MetaPayloads.StatusUpdate);

        result.Messages.Should().BeEmpty();
        result.SkippedTypes.Should().BeEmpty(); // a status update is not a skipped message
    }

    [Fact] // WP-04
    public void Parse_UnsupportedMessageType_IsSkipped() =>
        Parse(MetaPayloads.Image("wamid.A3")).Messages.Should().BeEmpty();

    [Fact] // WP-05
    public void Parse_TwoMessagesInOnePost_ReturnsBoth()
    {
        const string json = """
            { "entry": [ { "changes": [ { "value": { "messages": [
                { "from": "920000000001", "id": "wamid.B1", "type": "text", "text": { "body": "le li" } },
                { "from": "920000000002", "id": "wamid.B2", "type": "text", "text": { "body": "nahi" } } ] } } ] } ] }
            """;

        Parse(json).Messages.Select(m => m.WhatsappMessageId).Should().Equal("wamid.B1", "wamid.B2");
    }

    [Theory] // WP-06: incomplete or oddly shaped JSON never throws
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("""{ "entry": "x" }""")]
    [InlineData("""{ "entry": [ { "changes": [ { } ] } ] }""")]
    [InlineData("""{ "entry": [ { "changes": [ { "value": { "messages": [ { "type": "text" } ] } } ] } ] }""")]
    [InlineData("""{ "entry": [ { "changes": [ { "value": { "messages": [ { "id": "w", "from": "9", "type": "text", "text": {} } ] } } ] } ] }""")]
    public void Parse_IncompleteShapes_ReturnsNothingWithoutThrowing(string json) =>
        Parse(json).Messages.Should().BeEmpty();

    [Fact] // WP-07
    public void Parse_InvalidJson_ThrowsJsonException()
    {
        var act = () => Parse("{ not json");

        act.Should().Throw<JsonException>();
    }

    [Theory] // WP-08: a bad timestamp must not lose the message
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("99999999999999999")]
    [InlineData("")]
    public void Parse_InvalidTimestamp_KeepsMessageWithNullSentAt(string timestamp)
    {
        var json = $$"""
            { "entry": [ { "changes": [ { "value": { "messages": [
                { "from": "920000000001", "id": "wamid.C1", "timestamp": "{{timestamp}}", "type": "text", "text": { "body": "le li" } } ] } } ] } ] }
            """;

        var message = Parse(json).Messages.Should().ContainSingle().Subject;

        message.SentAt.Should().BeNull();
        message.ReceivedAt.Should().Be(Now);
    }

    [Fact] // WP-09: skipped types are reported so the endpoint can log them (a "button" reply is otherwise invisible)
    public void Parse_UnsupportedTypes_AreReportedInSkippedTypes()
    {
        var result = Parse(MetaPayloads.Image("wamid.D1"));

        result.SkippedTypes.Should().Equal("image");
    }
}
