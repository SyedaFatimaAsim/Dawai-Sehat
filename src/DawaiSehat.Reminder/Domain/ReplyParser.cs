using System.Globalization;
using System.Text;

namespace DawaiSehat.Reminder.Domain;

/// <summary>
/// Rule-based reading of a patient's reply (Roman Urdu or Urdu script). No AI, no database, no network.
/// Gemini is only a fallback for replies this class returns <see cref="ReplyIntent.Unknown"/> for.
/// </summary>
/// <remarks>
/// ORDER MATTERS. The checks run from most to least important:
/// 1. Unwell   - safety first: "le li lekin tabiyat kharab hai" must alert the caregiver.
/// 2. Later    - before NotTaken, because "abhi nahi" contains "nahi" but means "later".
/// 3. NotTaken - before Taken, because "nahi li" contains "li" but means "not taken".
/// 4. Taken
/// Matching is on whole words only, so "kali" does not match "li".
/// </remarks>
public static class ReplyParser
{
    // One word is enough to count as unwell. A false alarm only costs the caregiver a phone call;
    // a missed alarm could cost far more, so the list leans towards alerting.
    private static readonly string[] UnwellWords =
    [
        "chakkar", "bimar", "bemar", "bimari", "bemari", "dard", "saans", "ulti", "bukhar",
        "behosh", "behoshi", "ghabrahat",
        "چکر", "بیمار", "بیماری", "درد", "سانس", "الٹی", "بخار", "بیہوش", "گھبراہٹ",
    ];

    // "tabiyat" alone is neutral ("tabiyat theek hai" = I am fine), so it only counts together with a bad word.
    private static readonly string[] TabiyatWords = ["tabiyat", "tabiat", "tabyat", "tabeeyat", "طبیعت"];
    private static readonly string[] BadWords = ["kharab", "kharabi", "bigar", "خراب"];
    private static readonly string[] NotWellPhrases =
        ["theek nahi", "thik nahi", "acchi nahi", "achi nahi", "ٹھیک نہیں", "اچھی نہیں"];

    private static readonly string[] LaterPhrases =
    [
        "baad mein", "baad me", "baad main", "bad mein", "bad me",
        "thori dair", "thodi dair", "thori der", "thodi der", "kuch dair", "kuch der",
        "abhi nahi", "abhi nahin", "abhi nhi", "later",
        "بعد میں", "تھوڑی دیر", "کچھ دیر", "ابھی نہیں",
    ];

    private static readonly string[] NotTakenPhrases =
    [
        "nahi", "nahin", "nhi", "bhool", "bhul",
        "نہیں", "نہی", "بھول",
    ];

    private static readonly string[] TakenPhrases =
    [
        "le li", "le liya", "lee li", "kha li", "kha liya", "li", "liya", "lia",
        "haan", "han", "hn", "ji", "jee", "done", "yes",
        "لے لی", "لے لیا", "کھا لی", "کھا لیا", "لی", "لیا", "ہاں", "جی",
    ];

    public static ReplyIntent Parse(string? text)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0) return ReplyIntent.Unknown;

        if (IsUnwell(normalized)) return ReplyIntent.Unwell;
        if (ContainsAny(normalized, LaterPhrases)) return ReplyIntent.Later;
        if (ContainsAny(normalized, NotTakenPhrases)) return ReplyIntent.NotTaken;
        if (ContainsAny(normalized, TakenPhrases)) return ReplyIntent.Taken;

        return ReplyIntent.Unknown;
    }

    private static bool IsUnwell(string normalized) =>
        ContainsAny(normalized, UnwellWords)
        || (ContainsAny(normalized, TabiyatWords)
            && (ContainsAny(normalized, BadWords) || ContainsAny(normalized, NotWellPhrases)));

    // The text is padded with a space on both sides, so " li " finds the whole word "li" at any position.
    private static bool ContainsAny(string normalized, string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (normalized.Contains($" {phrase} ", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>
    /// Lower-case, remove Urdu diacritics, unify look-alike Arabic/Urdu letters, turn punctuation and emoji
    /// into single spaces. Returns "" when nothing readable is left, otherwise " word word ".
    /// </summary>
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var sb = new StringBuilder(text.Length + 2);
        sb.Append(' ');
        foreach (var original in text.ToLowerInvariant())
        {
            var c = original switch
            {
                'ي' or 'ى' => 'ی',  // Arabic yeh -> Urdu yeh
                'ك' => 'ک',          // Arabic kaf -> Urdu kaf
                'ه' or 'ۃ' => 'ہ',   // Arabic heh -> Urdu heh
                _ => original,
            };

            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue; // zabar, zer, pesh
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb[^1] != ' ') sb.Append(' ');
        }

        if (sb[^1] != ' ') sb.Append(' ');
        return sb.Length == 1 ? "" : sb.ToString();
    }
}
