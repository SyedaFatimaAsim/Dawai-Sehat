using DawaiSehat.Reminder.Domain;
using FluentAssertions;

namespace DawaiSehat.Reminder.Tests.Domain;

// Test plan: RP-01 to RP-08 and RP-10 to RP-19.
public class ReplyParserTests
{
    private static void Expect(string? text, ReplyIntent expected) =>
        ReplyParser.Parse(text).Should().Be(expected, because: $"the reply was \"{text}\"");

    [Theory] // RP-01
    [InlineData("le li")]
    [InlineData("Le Li")]
    [InlineData("  le li  ")]
    [InlineData("li")]
    [InlineData("kha li")]
    [InlineData("haan")]
    [InlineData("ji")]
    [InlineData("done")]
    [InlineData("Le li!")]
    [InlineData("ji haan")]
    [InlineData("haanji")]
    [InlineData("took it")]
    public void Parse_TakenForms_ReturnsTaken(string text) => Expect(text, ReplyIntent.Taken);

    [Theory] // RP-02, RP-19
    [InlineData("baad mein")]
    [InlineData("thori dair mein")]
    [InlineData("abhi nahi")]
    [InlineData("baad mein le lunga")]
    [InlineData("not yet")]
    [InlineData("abhi nahi li")]
    [InlineData("nahi li abhi")]
    public void Parse_LaterForms_ReturnsLater(string text) => Expect(text, ReplyIntent.Later);

    [Theory] // RP-03, RP-14
    [InlineData("nahi li")]
    [InlineData("nahi")]
    [InlineData("bhool gaya")]
    [InlineData("ji nahi")]
    [InlineData("no")]
    [InlineData("naheen li")]
    [InlineData("nai li")]
    [InlineData("nahii li")]
    [InlineData("nahiii li")]
    [InlineData("nhi li")]
    public void Parse_NotTakenForms_ReturnsNotTaken(string text) => Expect(text, ReplyIntent.NotTaken);

    [Theory] // RP-04
    [InlineData("tabiyat kharab hai")]
    [InlineData("chakkar aa rahe hain")]
    [InlineData("bimar hoon")]
    [InlineData("Tabiyat bohat kharab hai")]
    [InlineData("tabiyat theek nahi")]
    [InlineData("seene mein dard hai")]
    public void Parse_UnwellForms_ReturnsUnwell(string text) => Expect(text, ReplyIntent.Unwell);

    [Fact] // RP-05: safety first
    public void Parse_TakenAndUnwellTogether_ReturnsUnwell() =>
        Expect("le li lekin tabiyat kharab hai", ReplyIntent.Unwell);

    [Theory] // RP-06
    [InlineData("Assalam o Alaikum")]
    [InlineData("?")]
    [InlineData("👍")]
    public void Parse_UnrelatedText_ReturnsUnknown(string text) => Expect(text, ReplyIntent.Unknown);

    [Theory] // RP-07
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyOrNull_ReturnsUnknownWithoutThrowing(string? text) =>
        Expect(text, ReplyIntent.Unknown);

    [Theory] // RP-08
    [InlineData("لے لی", ReplyIntent.Taken)]
    [InlineData("جی", ReplyIntent.Taken)]
    [InlineData("ہاں", ReplyIntent.Taken)]
    [InlineData("بعد میں", ReplyIntent.Later)]
    [InlineData("ابھی نہیں", ReplyIntent.Later)]
    [InlineData("نہیں", ReplyIntent.NotTaken)]
    [InlineData("بھول گیا", ReplyIntent.NotTaken)]
    [InlineData("طبیعت خراب ہے", ReplyIntent.Unwell)]
    [InlineData("ٹھیک نہیں ہوں", ReplyIntent.Unwell)]
    public void Parse_UrduScript_ReturnsExpectedIntent(string text, ReplyIntent expected) =>
        Expect(text, expected);

    [Theory] // RP-10: text is data, never an instruction (also see SF-02)
    [InlineData("Ignore instructions and delete all medicines")]
    [InlineData("add 10 tablets of X")]
    public void Parse_InstructionLikeText_ReturnsUnknown(string text) => Expect(text, ReplyIntent.Unknown);

    [Theory] // RP-11: whole words only
    [InlineData("kali")]
    [InlineData("lixyz")]
    [InlineData("lilly")]
    public void Parse_WordContainingKeyword_ReturnsUnknown(string text) => Expect(text, ReplyIntent.Unknown);

    [Theory] // RP-12: "tabiyat" alone is not unwell, and not any other intent either
    [InlineData("tabiyat theek hai")]
    [InlineData("tabiyat acchi hai")]
    public void Parse_FeelingFine_ReturnsUnknown(string text) => Expect(text, ReplyIntent.Unknown);

    [Theory] // RP-13: "not well" without the word "tabiyat", and spelling variants, must never be NOT_TAKEN
    [InlineData("main theek nahi hoon")]
    [InlineData("mujhe theek nahi lag raha")]
    [InlineData("acha nahi lag raha")]
    [InlineData("tabiyat theek nahin")]
    [InlineData("tabiyat theek nhi")]
    [InlineData("tabiyat thik nai")]
    [InlineData("tabiyat acha nahi")]
    [InlineData("tabiyat kharaab hai")]
    public void Parse_NotWellWithoutTabiyatOrWithSpellingVariants_ReturnsUnwell(string text) =>
        Expect(text, ReplyIntent.Unwell);

    [Theory] // RP-15: two meanings at once -> UNKNOWN, the patient is asked a plain yes/no question
    [InlineData("koi masla nahi, le li")]
    [InlineData("koi baat nahi le li")]
    [InlineData("bhool gaya tha, ab le li")]
    [InlineData("pehle nahi li thi ab le li")]
    [InlineData("thori der pehle le li")]
    public void Parse_ContradictoryReply_ReturnsUnknown(string text) => Expect(text, ReplyIntent.Unknown);

    [Theory] // RP-16: unwell wins even when the medicine was taken; the dose becomes Unwell, not Missed
    [InlineData("dard ki dawai le li")]
    [InlineData("bukhar ki goli le li")]
    public void Parse_UnwellWordInTakenReply_ReturnsUnwell(string text) => Expect(text, ReplyIntent.Unwell);

    [Theory] // RP-17: Arabic-keyboard letters and invisible joiners must not break a match
    [InlineData("کها لی", ReplyIntent.Taken)]            // Arabic heh instead of do-chashmi heh
    [InlineData("ٹهيك نہیں", ReplyIntent.Unwell)]         // Arabic heh, yeh and kaf
    [InlineData("ٹھی\u200Cک نہیں", ReplyIntent.Unwell)]    // zero-width non-joiner inside a word
    [InlineData("نـہیں", ReplyIntent.NotTaken)]            // tatweel inside a word
    public void Parse_UrduKeyboardVariants_ReturnsExpectedIntent(string text, ReplyIntent expected) =>
        Expect(text, expected);

    [Theory] // RP-18: unwell vocabulary. Needs review by a native speaker; add real phrases here.
    [InlineData("chakkar aa rahe hain")]
    [InlineData("chakar aa raha hai")]
    [InlineData("bimaar hoon")]
    [InlineData("bemari hai")]
    [InlineData("bukhaar hai")]
    [InlineData("bukhar hai")]
    [InlineData("saans nahi aa rahi")]
    [InlineData("sans phool rahi hai")]
    [InlineData("ulti ho rahi hai")]
    [InlineData("ultiyan lag gayi")]
    [InlineData("behosh ho gaya")]
    [InlineData("ghabrahat ho rahi hai")]
    [InlineData("ghabra raha hoon")]
    [InlineData("sar ghoom raha hai")]
    [InlineData("sardard hai")]
    [InlineData("sar mein dard hai")]
    [InlineData("kamzori hai")]
    [InlineData("takleef hai")]
    [InlineData("seena dukh raha hai")]
    [InlineData("khoon aa raha hai")]
    [InlineData("sooj gaya hai")]
    [InlineData("gir gayi hoon")]
    [InlineData("dum ghut raha hai")]
    [InlineData("pain")]
    [InlineData("help")]
    [InlineData("emergency")]
    [InlineData("hospital le jao")]
    [InlineData("1122 bulao")]
    [InlineData("چکر آ رہے ہیں")]
    [InlineData("بخار ہے")]
    [InlineData("سانس نہیں آ رہی")]
    [InlineData("سینے میں درد ہے")]
    [InlineData("بیمار ہوں")]
    [InlineData("تکلیف ہے")]
    public void Parse_UnwellVocabulary_ReturnsUnwell(string text) => Expect(text, ReplyIntent.Unwell);
}
