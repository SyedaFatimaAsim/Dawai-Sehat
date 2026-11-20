using System.Security.Cryptography;

namespace DawaiSehat.Reminder.Features.ReceiveReplies;

/// <summary>
/// Checks that a webhook call really comes from Meta. Meta computes HMAC-SHA256 of the raw request body
/// with our App Secret and sends it as "X-Hub-Signature-256: sha256=&lt;hex&gt;". Only Meta and we know the secret,
/// so a matching signature proves the body was not forged or changed.
/// </summary>
public static class WebhookSignature
{
    private const string Prefix = "sha256=";

    public static bool IsValid(ReadOnlySpan<byte> body, string? header, string? appSecret)
    {
        // Fail closed: no secret configured or no header sent means we cannot trust the call.
        if (string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(header)) return false;
        if (!header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;

        byte[] received;
        try
        {
            received = Convert.FromHexString(header.AsSpan(Prefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(appSecret), body);

        // Constant-time comparison: an attacker cannot learn the signature byte by byte from response times.
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }

    /// <summary>Builds the header value Meta would send. Used by tests.</summary>
    public static string Compute(ReadOnlySpan<byte> body, string appSecret) =>
        Prefix + Convert.ToHexStringLower(HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(appSecret), body));
}
