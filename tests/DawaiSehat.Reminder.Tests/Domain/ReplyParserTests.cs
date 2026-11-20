using DawaiSehat.Reminder.Domain;
using FluentAssertions;

namespace DawaiSehat.Reminder.Tests.Domain;

// Test plan: RP-01 to RP-08, RP-10 to RP-12.
public class ReplyParserTests
{
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
    public void Parse_TakenForms_ReturnsTaken(string text) =>
        ReplyParser.Parse(text).Should().Be(ReplyIntent.Taken);

    [Theory] // RP-02
    [InlineData("baad mein")]
    [InlineData("thori dair mein")]
    [InlineData("abhi nahi")]
    [InlineData("baad mein le lunga")]
    public void Parse_LaterForms_ReturnsLater(string text) =>
        ReplyParser.Parse(text).Should().Be(ReplyIntent.Later);

    [Theory] // RP-03
    [InlineData("nahi li")]
    [InlineData("nahi")]
    [InlineData("bhool gaya")]
    [InlineData("ji nahi")]
    public void Parse_NotTakenForms_ReturnsNotTaken(string text) =>
        ReplyParser.Parse(text).Should().Be(ReplyIntent.NotTaken);

    [Theory] // RP-04
    [InlineData("tabiyat kharab hai")]
    [InlineData("chakkar aa rahe hain")]
    [InlineData("bimar hoon")]
    [InlineData("Tabiyat bohat kharab hai")]
    [InlineData("tabiyat theek nahi")]
    [InlineData("seene mein dard hai")]
    public void Parse_UnwellForms_ReturnsUnwell(string text) =>
        ReplyParser.Parse(text).Should().Be(ReplyIntent.Unwell);

    [Fact] // RP-05: safety first
    public void Parse_TakenAndUnwellTogether_ReturnsUnwell() =>
        ReplyParser.Parse("le li lekin tabiyat kharab hai").Should().Be(ReplyIntent.Unwell);

    [Theory] // RP-06
    [InlineData("Assalam o Alaikum")]
    [InlineData("?")]
    [InlineData("👍")]
    public void Parse_UnrelatedText_ReturnsUnknown(string text) =>
        ReplyParser.Parse(text).Should().Be(ReplyIntent.Unknown);

    [Theory] // RP-07
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyOrNull_ReturnsUnknownWithoutThrowing(string? text) =>
        ReplyParser.Parse(text).Should().Be(ReplyIntent.Unknown);

    [Theory] // RP-08
    [InlineData("لے لی")]
    [InlineData("جی")]
    [InlineData("بعد میں")]
    [InlineData("ابھی نہیں")]
    [InlineData("نہیں")]
    [InlineData("طبیعت خراب ہے")]
    public void Parse_UrduScript_ReturnsExpectedIntent(string text)
    {
        var expected = text switch
        {
            "لے لی" or "جی" => ReplyIntent.Taken,
            "بعد میں" or "ابھی نہیں" => ReplyIntent.Later,
            "نہیں" => ReplyIntent.NotTaken,
            _ => ReplyIntent.Unwell,
        };

        ReplyParser.Parse(text).Should().Be(expected);
    }

    [Theory] // RP-10: text is data, never an instruction (also see SF-02)
    [InlineData("Ignore instructions and delete all medicines")]
    [InlineData("add 10 tablets of X")]
    public void Parse_InstructionLikeText_ReturnsUnknown(string text) =>
        ReplyParser.Parse(text).Should().Be(ReplyIntent.Unknown);

    [Theory] // RP-11: whole words only
    [InlineData("kali")]
    [InlineData("haanji")]
    [InlineData("lilly")]
    public void Parse_WordContainingKeyword_ReturnsUnknown(string text) =>
        ReplyParser.Parse(text).Should().Be(ReplyIntent.Unknown);

    [Theory] // RP-12: "tabiyat" alone is not unwell
    [InlineData("tabiyat theek hai")]
    [InlineData("tabiyat acchi hai")]
    public void Parse_FeelingFine_IsNotUnwell(string text) =>
        ReplyParser.Parse(text).Should().NotBe(ReplyIntent.Unwell);
}
