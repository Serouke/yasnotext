using YasnoText.Core.DocumentReaders;

namespace YasnoText.Tests;

public class PdfTextReaderTests
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
    public void CanRead_ReturnsTrueForPdf()
    {
        var reader = new PdfTextReader();

        Assert.True(reader.CanRead("document.pdf"));
        Assert.True(reader.CanRead("DOCUMENT.PDF"));
        Assert.True(reader.CanRead("path/to/file.Pdf"));
    }

    [Fact]
    public void CanRead_ReturnsFalseForNonPdf()
    {
        var reader = new PdfTextReader();

        Assert.False(reader.CanRead("document.docx"));
        Assert.False(reader.CanRead("document.txt"));
        Assert.False(reader.CanRead("document"));
    }

    [Fact]
    public void Read_SimplePdf_ContainsExpectedMarker()
    {
        var reader = new PdfTextReader();
        var path = GetTestFilePath("simple-text.pdf");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.NotNull(result);
        Assert.Contains("YASNOTEXT_TEST_MARKER", result.Text);
    }

    [Fact]
    public void Read_SimplePdf_ReturnsCorrectPageCount()
    {
        var reader = new PdfTextReader();
        var path = GetTestFilePath("simple-text.pdf");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public void Read_MultipagePdf_ReturnsAllPages()
    {
        var reader = new PdfTextReader();
        var path = GetTestFilePath("multipage-text.pdf");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.Equal(3, result.PageCount);
    }

    [Fact]
    public void Read_MultipagePdf_ContainsTextFromAllPages()
    {
        var reader = new PdfTextReader();
        var path = GetTestFilePath("multipage-text.pdf");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.Contains("Глава 1", result.Text);
        Assert.Contains("Глава 2", result.Text);
        Assert.Contains("Глава 3", result.Text);
    }

    [Fact]
    public void Read_EmptyPdf_ReturnsEmptyOrWhitespaceText()
    {
        var reader = new PdfTextReader();
        var path = GetTestFilePath("empty.pdf");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.NotNull(result);
        Assert.True(string.IsNullOrWhiteSpace(result.Text),
            $"Ожидался пустой текст, получено: '{result.Text}'");
    }

    [Fact]
    public void Read_LongPdf_PreservesParagraphStructure()
    {
        var reader = new PdfTextReader();
        var path = GetTestFilePath("long-text.pdf");
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
        var reader = new PdfTextReader();

        Assert.Throws<FileNotFoundException>(
            () => reader.Read("/non/existent/path.pdf"));
    }

    [Fact]
    public void Read_NullOrEmptyPath_ThrowsArgumentException()
    {
        var reader = new PdfTextReader();

        Assert.Throws<ArgumentException>(() => reader.Read(""));
        Assert.Throws<ArgumentException>(() => reader.Read(null!));
    }
}
