using YasnoText.Core.DocumentReaders;
using YasnoText.Core.Ocr;

namespace YasnoText.Tests;

public class OcrImageReaderTests : IDisposable
{
    private readonly TesseractOcrService _ocr;

    public OcrImageReaderTests()
    {
        // Языковые модели копируются в bin/.../tessdata через csproj.
        var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        _ocr = new TesseractOcrService(tessdataPath, "eng+rus");
    }

    public void Dispose()
    {
        _ocr.Dispose();
    }

    private static string GetTestFilePath(string fileName)
    {
        var testFilesDir = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "test-files");
        return Path.GetFullPath(Path.Combine(testFilesDir, fileName));
    }

    [Fact]
    public void CanRead_ReturnsTrueForSupportedImageExtensions()
    {
        var reader = new OcrImageReader(_ocr);

        Assert.True(reader.CanRead("photo.png"));
        Assert.True(reader.CanRead("scan.jpg"));
        Assert.True(reader.CanRead("scan.JPEG"));
        Assert.True(reader.CanRead("page.tif"));
        Assert.True(reader.CanRead("page.tiff"));
        Assert.True(reader.CanRead("photo.bmp"));
    }

    [Fact]
    public void CanRead_ReturnsFalseForNonImageExtensions()
    {
        var reader = new OcrImageReader(_ocr);

        Assert.False(reader.CanRead("document.pdf"));
        Assert.False(reader.CanRead("document.docx"));
        Assert.False(reader.CanRead("photo"));
        Assert.False(reader.CanRead(""));
    }

    [Fact]
    public void Read_NonExistentFile_ThrowsFileNotFoundException()
    {
        var reader = new OcrImageReader(_ocr);

        Assert.Throws<FileNotFoundException>(
            () => reader.Read("/non/existent/image.png"));
    }

    [Fact]
    public void Read_NullOrEmptyPath_ThrowsArgumentException()
    {
        var reader = new OcrImageReader(_ocr);

        Assert.Throws<ArgumentException>(() => reader.Read(""));
        Assert.Throws<ArgumentException>(() => reader.Read(null!));
    }

    [Fact]
    public void Read_EnglishImage_ContainsExpectedMarker()
    {
        var reader = new OcrImageReader(_ocr);
        var path = GetTestFilePath("image-english.png");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.NotNull(result);
        Assert.Equal(1, result.PageCount);
        Assert.Contains("YASNOTEXT_TEST_MARKER", result.Text);
    }

    [Fact]
    public void Read_RussianImage_ContainsRussianText()
    {
        var reader = new OcrImageReader(_ocr);
        var path = GetTestFilePath("image-russian.png");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        // Tesseract на чистом шрифте даёт высокую точность,
        // но «Привет, мир!» может распознаться без запятой / знака. Проверяем
        // лишь, что ключевые слова на месте.
        Assert.Contains("Привет", result.Text);
        Assert.Contains("мир", result.Text);
    }

    [Fact]
    public void Read_ParagraphImage_ContainsAtLeastOneLine()
    {
        var reader = new OcrImageReader(_ocr);
        var path = GetTestFilePath("image-paragraph.png");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.NotNull(result);
        // Хотя бы одна из исходных фраз должна распознаться полностью.
        var hasAnyLine =
            result.Text.Contains("тестовый абзац") ||
            result.Text.Contains("Вторая строка") ||
            result.Text.Contains("Третья строка");
        Assert.True(hasAnyLine,
            $"Ни одна строка не распознана. OCR-вывод: '{result.Text}'");
    }

    [Fact]
    public void Read_EmptyImage_ReturnsIsEmpty()
    {
        var reader = new OcrImageReader(_ocr);
        var path = GetTestFilePath("image-empty.png");
        Assert.True(File.Exists(path), $"Тестовый файл не найден: {path}");

        var result = reader.Read(path);

        Assert.NotNull(result);
        Assert.True(result.IsEmpty,
            $"Ожидался пустой результат, получено: '{result.Text}'");
    }
}
