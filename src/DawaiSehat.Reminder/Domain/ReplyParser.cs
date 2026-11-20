using System.Globalization;
using System.Text;

namespace DawaiSehat.Reminder.Domain;

/// <summary>
/// Rule-based reading of a patient's reply (Roman Urdu, Urdu script, a little English).
/// No AI, no database, no network. Gemini is only a fallback for replies this class returns
/// <see cref="ReplyIntent.Unknown"/> for.
/// </summary>
/// <remarks>
/// How a reply is decided (most important rule first):
/// 1. UNWELL always wins. Any unwell word alerts the caregiver, even in "dard ki dawai le li".
///    A false alarm costs one phone call; a missed alarm could cost far more.
///    (The dose then becomes Unwell, not Missed, so no missed-dose alert is raised on top.)
/// 2. If the reply says "I took it" AND "not / later" ("koi masla nahi, le li", "thori der pehle le li"),
///    we return UNKNOWN. Two meanings fighting each other is not something to guess: the caller asks
///    the patient a plain yes/no question instead.
/// 3. LATER, then NOT_TAKEN, then TAKEN. "abhi nahi" and "nahi li abhi" mean "not yet", so they are LATER.
///    "nahi li" contains "li" but is NOT_TAKEN.
/// Matching is on whole words, so "kali" does not match "li". Spelling variants are listed by hand;
/// anything we do not know becomes UNKNOWN. A bare "ji" or "haan" is TAKEN because the caller only
/// parses replies to a pending dose question.
/// The word lists are a safety asset: they need review by a native speaker.
/// </remarks>
public static class ReplyParser
{
    // ---- Unwell -----------------------------------------------------------------------------------
    // One word is enough. The list leans towards alerting.
    private static readonly string[] UnwellWords = Prepare(
        "chakkar", "chakar", "bimar", "bimaar", "bemar", "bimari", "bemari", "dard", "sardard",
        "saans", "sans", "saas", "ulti", "ultiyan", "bukhar", "bukhaar", "behosh", "behoshi",
        "ghabrahat", "ghabra", "ghoom", "kamzori", "kamzor", "takleef", "khoon", "sooj", "seena",
        "allergy", "reaction", "pain", "help", "emergency", "hospital", "ambulance", "1122",
        "چکر", "بیمار", "بیماری", "درد", "سانس", "الٹی", "بخار", "بیہوش", "گھبراہٹ", "کمزوری",
        "تکلیف", "خون", "سوجن", "سینہ");

    private static readonly string[] UnwellPhrases = Prepare(
        "gir gaya", "gir gayi", "gir gaye", "dum ghut", "sine mein", "گر گیا", "گر گئی");

    // "tabiyat" alone is neutral ("tabiyat theek hai" = I am fine), so it only counts with a bad word.
    private static readonly string[] TabiyatWords =
        Prepare("tabiyat", "tabiat", "tabyat", "tabeeyat", "tabeyat", "tabiyath", "طبیعت");
    private static readonly string[] BadWords = Prepare("kharab", "kharaab", "kharabi", "bigar", "bigad", "خراب");

    // Negation spellings people really type. Shared by several rules below.
    private static readonly string[] NegativeWords = Prepare(
        "nahi", "nahin", "nhi", "nhin", "nai", "nae", "nehi", "naheen", "nahee", "nahii", "نہیں", "نہی");
    private static readonly string[] OkWords = Prepare(
        "theek", "thik", "acha", "achha", "accha", "acchi", "achi", "achhi", "ٹھیک", "اچھا", "اچھی");

    // "theek nahi", "acha nahin lag raha" ... on their own they already mean "I am not well".
    private static readonly string[] NotWellPhrases = BuildNotWellPhrases();

    // ---- Later / not taken / taken --------------------------------------------------------------
    private static readonly string[] AbhiWords = Prepare("abhi", "ابھی");

    private static readonly string[] LaterPhrases = Prepare(
        "baad mein", "baad me", "baad main", "bad mein", "bad me",
        "thori dair", "thodi dair", "thori der", "thodi der", "kuch dair", "kuch der",
        "later", "not yet",
        "بعد میں", "تھوڑی دیر", "کچھ دیر");

    private static readonly string[] NotTakenPhrases = Prepare(
        "bhool", "bhul", "no", "nope", "بھول");

    // Clear "I took it" wording. Together with a negation it is a contradiction -> UNKNOWN.
    private static readonly string[] StrongTakenPhrases = Prepare(
        "le li", "le liya", "lee li", "lee liya", "kha li", "kha liya", "took it", "taken",
        "لے لی", "لے لیا", "کھا لی", "کھا لیا");

    // Short answers that mean "yes, taken" when given to the dose question.
    private static readonly string[] WeakTakenPhrases = Prepare(
        "li", "liya", "lia", "haan", "han", "hn", "ji", "jee", "haanji", "hanji", "done", "yes",
        "لی", "لیا", "ہاں", "جی");

    public static ReplyIntent Parse(string? text)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0) return ReplyIntent.Unknown;

        if (IsUnwell(normalized)) return ReplyIntent.Unwell;

        var later = ContainsAny(normalized, LaterPhrases)
                    || (ContainsAny(normalized, AbhiWords) && ContainsAny(normalized, NegativeWords));
        var notTaken = ContainsAny(normalized, NegativeWords) || ContainsAny(normalized, NotTakenPhrases);
        var strongTaken = ContainsAny(normalized, StrongTakenPhrases);
        var weakTaken = ContainsAny(normalized, WeakTakenPhrases);

        if (strongTaken && (later || notTaken)) return ReplyIntent.Unknown; // contradiction: do not guess
        if (later) return ReplyIntent.Later;
        if (notTaken) return ReplyIntent.NotTaken;
        if (strongTaken || weakTaken) return ReplyIntent.Taken;

        return ReplyIntent.Unknown;
    }

    private static bool IsUnwell(string normalized) =>
        ContainsAny(normalized, UnwellWords)
        || ContainsAny(normalized, UnwellPhrases)
        || ContainsAny(normalized, NotWellPhrases)
        || (ContainsAny(normalized, TabiyatWords) && ContainsAny(normalized, BadWords));

    private static string[] BuildNotWellPhrases()
    {
        // Every "theek"/"acha" word combined with every negation: "theek nahi", "acha nhi", "ٹھیک نہیں" ...
        var phrases = new List<string>();
        foreach (var ok in OkWords)
            foreach (var negative in NegativeWords)
                phrases.Add(ok.Trim() + " " + negative.Trim());
        return Prepare(phrases.ToArray());
    }

    // Phrases are padded with a space on both sides, as is the reply, so " li " matches the whole word only.
    private static bool ContainsAny(string normalized, string[] paddedPhrases)
    {
        foreach (var phrase in paddedPhrases)
        {
            if (normalized.Contains(phrase, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    // Phrase lists go through the same Normalize as the reply, so both sides always look the same.
    private static string[] Prepare(params string[] phrases) =>
        phrases.Select(Normalize).Where(p => p.Length > 0).ToArray();

    /// <summary>
    /// Lower-case, fold look-alike Arabic/Urdu letters, drop diacritics and invisible joiners, turn punctuation
    /// and emoji into single spaces, shorten letters repeated 3+ times ("nahiii" -> "nahii").
    /// Returns "" when nothing readable is left, otherwise " word word ".
    /// </summary>
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var sb = new StringBuilder(text.Length + 2);
        sb.Append(' ');
        var run = 0;
        foreach (var original in text.ToLowerInvariant())
        {
            var c = original switch
            {
                'ي' or 'ى' => 'ی',       // Arabic yeh -> Urdu yeh
                'ك' => 'ک',               // Arabic kaf -> Urdu kaf
                'ه' or 'ۃ' or 'ھ' => 'ہ', // Arabic heh, round heh, do-chashmi heh -> Urdu heh
                _ => original,
            };

            // Invisible joiners (ZWNJ, ZWJ, direction marks) and tatweel sit inside words: drop, do not split.
            if (c is '\u200C' or '\u200D' or '\u200E' or '\u200F' or '\u0640') continue;
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue; // zabar, zer, pesh

            if (char.IsLetterOrDigit(c))
            {
                run = sb[^1] == c ? run + 1 : 1;
                if (run <= 2) sb.Append(c);
            }
            else if (sb[^1] != ' ')
            {
                sb.Append(' ');
                run = 0;
            }
        }

        if (sb[^1] != ' ') sb.Append(' ');
        return sb.Length == 1 ? "" : sb.ToString();
    }
}
