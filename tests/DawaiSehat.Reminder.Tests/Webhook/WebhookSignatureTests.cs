using System.Text;
using DawaiSehat.Reminder.Features.ReceiveReplies;
using FluentAssertions;

namespace DawaiSehat.Reminder.Tests.Webhook;

// Test plan: SG-01 to SG-06 (X-Hub-Signature-256 check).
public class WebhookSignatureTests
{
    private const string Secret = "test-app-secret";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"hello":"world"}""");

    [Fact] // SG-01
    public void IsValid_CorrectSignature_ReturnsTrue() =>
        WebhookSignature.IsValid(Body, WebhookSignature.Compute(Body, Secret), Secret).Should().BeTrue();

    [Fact] // SG-02
    public void IsValid_SignedWithOtherSecret_ReturnsFalse() =>
        WebhookSignature.IsValid(Body, WebhookSignature.Compute(Body, "other-secret"), Secret).Should().BeFalse();

    [Fact] // SG-03
    public void IsValid_BodyChangedAfterSigning_ReturnsFalse()
    {
        var header = WebhookSignature.Compute(Body, Secret);
        var tampered = Encoding.UTF8.GetBytes("""{"hello":"WORLD"}""");

        WebhookSignature.IsValid(tampered, header, Secret).Should().BeFalse();
    }

    [Theory] // SG-04
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=")]
    [InlineData("sha256=not-hex")]
    [InlineData("abcdef")]              // no "sha256=" prefix
    [InlineData("sha256=abcd")]         // valid hex, wrong length
    public void IsValid_MissingOrBrokenHeader_ReturnsFalseWithoutThrowing(string? header) =>
        WebhookSignature.IsValid(Body, header, Secret).Should().BeFalse();

    [Theory] // SG-05: fail closed when no secret is configured
    [InlineData(null)]
    [InlineData("")]
    public void IsValid_NoSecretConfigured_ReturnsFalse(string? secret) =>
        WebhookSignature.IsValid(Body, WebhookSignature.Compute(Body, "anything"), secret).Should().BeFalse();

    [Fact] // SG-07: the signature is over the raw bytes, so Urdu script must work too
    public void IsValid_UrduBody_ReturnsTrue()
    {
        var urdu = Encoding.UTF8.GetBytes("""{"text":"لے لی"}""");

        WebhookSignature.IsValid(urdu, WebhookSignature.Compute(urdu, Secret), Secret).Should().BeTrue();
    }

    [Fact] // SG-06
    public void IsValid_UppercaseHex_ReturnsTrue()
    {
        var header = "sha256=" + WebhookSignature.Compute(Body, Secret)["sha256=".Length..].ToUpperInvariant();

        WebhookSignature.IsValid(Body, header, Secret).Should().BeTrue();
    }
}
