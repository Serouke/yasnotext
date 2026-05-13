using YasnoText.Core.DocumentReaders;

namespace YasnoText.Tests;

public class DocxTextReaderTests
{
    private static string GetTestFilePath(string fileName)
    {
        // bin/Debug/net10.0 → корень репо: ../../../../..
        var testFilesDir = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "test-files");
        return Path.GetFullPath(Path.Combine(testFilesDir, fileName));
    }

    [Fact]
    public void CanRead_ReturnsTrueForDocx()
    {
        var reader = new DocxTextReader();

        Assert.True(reader.CanRead("document.docx"));
        Assert.True(reader.CanRead("DOCUMENT.DOCX"));
        Assert.True(reader.CanRead("path/to/file.Docx"));
    }

    [Fact]
    public void CanRead_ReturnsFalseForNonDocx()
    {
        var reader = new DocxTextReader();

        Assert.False(reader.CanRead("document.pdf"));
        Assert.False(reader.CanRead("document.doc")); // старый бинарный формат не поддерживается
        Assert.False(reader.CanRead("document.txt"));
        Assert.False(reader.CanRead("document"));
    }

    [Fact]
    public void Read_SimpleDocx_ContainsExpectedMarker()
    {
        var reader = new DocxTextReader();
        var path = GetTestFilePath("simple-text.docx");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.NotNull(result);
        Assert.Contains("YASNOTEXT_TEST_MARKER", result.Text);
    }

    [Fact]
    public void Read_SimpleDocx_ReturnsCorrectPageCount()
    {
        var reader = new DocxTextReader();
        var path = GetTestFilePath("simple-text.docx");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public void Read_MultipageDocx_ReturnsAllPages()
    {
        var reader = new DocxTextReader();
        var path = GetTestFilePath("multipage-text.docx");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.Equal(3, result.PageCount);
    }

    [Fact]
    public void Read_MultipageDocx_ContainsTextFromAllPages()
    {
        var reader = new DocxTextReader();
        var path = GetTestFilePath("multipage-text.docx");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.Contains("Глава 1", result.Text);
        Assert.Contains("Глава 2", result.Text);
        Assert.Contains("Глава 3", result.Text);
    }

    [Fact]
    public void Read_EmptyDocx_ReturnsEmptyOrWhitespaceText()
    {
        var reader = new DocxTextReader();
        var path = GetTestFilePath("empty.docx");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.NotNull(result);
        Assert.True(string.IsNullOrWhiteSpace(result.Text),
            $"Ожидался пустой текст, получено: '{result.Text}'");
    }

    [Fact]
    public void Read_LongDocx_PreservesParagraphStructure()
    {
        var reader = new DocxTextReader();
        var path = GetTestFilePath("long-text.docx");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        for (int i = 1; i <= 5; i++)
        {
            Assert.Contains($"Параграф №{i}", result.Text);
        }
    }

    [Fact]
    public void Read_NonExistentFile_ThrowsFileNotFoundException()
    {
        var reader = new DocxTextReader();

        Assert.Throws<FileNotFoundException>(
            () => reader.Read("/non/existent/path.docx"));
    }

    [Fact]
    public void Read_NullOrEmptyPath_ThrowsArgumentException()
    {
        var reader = new DocxTextReader();

        Assert.Throws<ArgumentException>(() => reader.Read(""));
        Assert.Throws<ArgumentException>(() => reader.Read(null!));
    }
}
