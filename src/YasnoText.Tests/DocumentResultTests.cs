using YasnoText.Core.DocumentReaders;

namespace YasnoText.Tests;

public class DocumentResultTests
{
    [Fact]
    public void Empty_ReturnsResultWithNoText()
    {
        var result = DocumentResult.Empty();

        Assert.Equal(string.Empty, result.Text);
        Assert.Equal(0, result.PageCount);
    }

    [Fact]
    public void Constructor_StoresTextAndPageCount()
    {
        var result = new DocumentResult("Hello, world!", 5);

        Assert.Equal("Hello, world!", result.Text);
        Assert.Equal(5, result.PageCount);
    }

    [Fact]
    public void IsEmpty_ReturnsTrueForEmptyText()
    {
        var empty = new DocumentResult("", 0);
        var whitespace = new DocumentResult("   \n\t  ", 1);

        Assert.True(empty.IsEmpty);
        Assert.True(whitespace.IsEmpty);
    }

    [Fact]
    public void IsEmpty_ReturnsFalseForNonEmptyText()
    {
        var result = new DocumentResult("Some text", 1);

        Assert.False(result.IsEmpty);
    }
}
