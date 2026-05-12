using YasnoText.Core.TextProcessing;

namespace YasnoText.Tests;

public class SentenceSplitterTests
{
    [Fact]
    public void Split_EmptyText_ReturnsEmpty()
    {
        var result = SentenceSplitter.Split("");
        Assert.Empty(result);
    }

    [Fact]
    public void Split_SingleSentence_WithoutPunctuation_ReturnsOne()
    {
        var result = SentenceSplitter.Split("Без точки");
        Assert.Single(result);
        Assert.Equal("Без точки", result[0].Text);
        Assert.Equal(0, result[0].Offset);
    }

    [Fact]
    public void Split_TwoSentences_DotSpace()
    {
        var text = "Привет мир. Как дела?";
        var result = SentenceSplitter.Split(text);

        Assert.Equal(2, result.Count);
        Assert.Equal("Привет мир.", result[0].Text);
        Assert.Equal(0, result[0].Offset);
        Assert.Equal("Как дела?", result[1].Text);
        Assert.Equal(12, result[1].Offset); // 11 символов + пробел
    }

    [Fact]
    public void Split_CollapsesEllipsis()
    {
        // «...» считается одним терминатором.
        var result = SentenceSplitter.Split("Так... И что дальше?");

        Assert.Equal(2, result.Count);
        Assert.Equal("Так...", result[0].Text);
        Assert.Equal("И что дальше?", result[1].Text);
    }

    [Fact]
    public void Split_DotInsideNumber_DoesNotBreak()
    {
        // «3.14» не должен резаться: после точки не пробел.
        var result = SentenceSplitter.Split("Пи равно 3.14");

        Assert.Single(result);
    }

    [Fact]
    public void Split_OffsetsAreCorrect()
    {
        var text = "Один. Два. Три.";
        var result = SentenceSplitter.Split(text);

        Assert.Equal(3, result.Count);
        foreach (var s in result)
        {
            Assert.Equal(s.Text, text.Substring(s.Offset, s.Length));
        }
    }

    [Fact]
    public void Split_MultipleExclamations()
    {
        var result = SentenceSplitter.Split("Ну!!! Что?!");

        Assert.Equal(2, result.Count);
        Assert.Equal("Ну!!!", result[0].Text);
        Assert.Equal("Что?!", result[1].Text);
    }
}
