using YasnoText.Core.TextProcessing;

namespace YasnoText.Tests;

public class TextPostProcessorTests
{
    [Fact]
    public void FixSoftHyphenLineBreaks_NullOrEmpty_ReturnsAsIs()
    {
        Assert.Equal(string.Empty, TextPostProcessor.FixSoftHyphenLineBreaks(string.Empty));
        Assert.Null(TextPostProcessor.FixSoftHyphenLineBreaks(null!));
    }

    [Fact]
    public void FixSoftHyphenLineBreaks_RussianWordSplitWithLf_IsJoined()
    {
        var input = "обуче-\nние";
        var output = TextPostProcessor.FixSoftHyphenLineBreaks(input);
        Assert.Equal("обучение", output);
    }

    [Fact]
    public void FixSoftHyphenLineBreaks_RussianWordSplitWithCrLf_IsJoined()
    {
        var input = "програм-\r\nмирование";
        var output = TextPostProcessor.FixSoftHyphenLineBreaks(input);
        Assert.Equal("программирование", output);
    }

    [Fact]
    public void FixSoftHyphenLineBreaks_EnglishWordSplit_IsJoined()
    {
        var input = "infor-\nmation";
        var output = TextPostProcessor.FixSoftHyphenLineBreaks(input);
        Assert.Equal("information", output);
    }

    [Fact]
    public void FixSoftHyphenLineBreaks_MultipleHyphenations_AllJoined()
    {
        var input =
            "пер-\nвый абзац.\n" +
            "Здесь ещё одна стро-\nка.";
        var output = TextPostProcessor.FixSoftHyphenLineBreaks(input);
        Assert.Equal("первый абзац.\nЗдесь ещё одна строка.", output);
    }

    [Fact]
    public void FixSoftHyphenLineBreaks_HyphenBetweenDigits_IsNotTouched()
    {
        // 2024-25 на стыке строк — это не слово, не клеим.
        var input = "период 2024-\n25";
        var output = TextPostProcessor.FixSoftHyphenLineBreaks(input);
        Assert.Equal(input, output);
    }

    [Fact]
    public void FixSoftHyphenLineBreaks_NormalLineBreakWithoutHyphen_IsPreserved()
    {
        // Просто перенос строки без дефиса — должен сохраниться.
        var input = "первая строка\nвторая строка";
        var output = TextPostProcessor.FixSoftHyphenLineBreaks(input);
        Assert.Equal(input, output);
    }

    [Fact]
    public void FixSoftHyphenLineBreaks_TextWithoutSoftHyphens_IsUnchanged()
    {
        var input = "Обычный текст без переносов слов.";
        var output = TextPostProcessor.FixSoftHyphenLineBreaks(input);
        Assert.Equal(input, output);
    }
}
