using System.Runtime.Versioning;
using YasnoText.Core.DocumentReaders;
using YasnoText.Core.Ocr;

namespace YasnoText.Tests;

[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public class OcrPdfReaderTests : IDisposable
{
    private readonly TesseractOcrService _ocr;

    public OcrPdfReaderTests()
    {
        var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        _ocr = new TesseractOcrService(tessdataPath, "eng+rus");
    }

    public void Dispose() => _ocr.Dispose();

    private static string GetTestFilePath(string fileName)
    {
        var testFilesDir = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "test-files");
        return Path.GetFullPath(Path.Combine(testFilesDir, fileName));
    }

    private sealed class FakeRenderer : IPdfRenderer
    {
        private readonly byte[][] _pages;
        public int RenderCalls { get; private set; }
        public FakeRenderer(params byte[][] pages) => _pages = pages;

        public int GetPageCount(string pdfPath) => _pages.Length;

        public byte[] RenderPageAsPng(string pdfPath, int pageIndex, int dpi = 300)
        {
            RenderCalls++;
            return _pages[pageIndex];
        }
    }

    private sealed class StubOcr : IOcrService
    {
        private readonly Func<int, string> _textForCall;
        private int _calls;

        public StubOcr(Func<int, string> textForCall) => _textForCall = textForCall;

        public string Recognize(string imagePath) => _textForCall(_calls++);
        public string Recognize(byte[] imageBytes) => _textForCall(_calls++);
        public void Dispose() { }
    }

    [Fact]
    public void CanRead_ReturnsTrueForPdf()
    {
        var reader = new OcrPdfReader(new FakeRenderer(), new StubOcr(_ => ""));
        Assert.True(reader.CanRead("scan.pdf"));
        Assert.True(reader.CanRead("scan.PDF"));
    }

    [Fact]
    public void CanRead_ReturnsFalseForNonPdf()
    {
        var reader = new OcrPdfReader(new FakeRenderer(), new StubOcr(_ => ""));
        Assert.False(reader.CanRead("scan.png"));
        Assert.False(reader.CanRead("doc.docx"));
        Assert.False(reader.CanRead(""));
    }

    [Fact]
    public void Read_NullOrEmptyPath_ThrowsArgumentException()
    {
        var reader = new OcrPdfReader(new FakeRenderer(), new StubOcr(_ => ""));
        Assert.Throws<ArgumentException>(() => reader.Read(""));
        Assert.Throws<ArgumentException>(() => reader.Read(null!));
    }

    [Fact]
    public void Read_NonExistentFile_ThrowsFileNotFoundException()
    {
        var reader = new OcrPdfReader(new FakeRenderer(), new StubOcr(_ => ""));
        Assert.Throws<FileNotFoundException>(() => reader.Read("/nope/scan.pdf"));
    }

    [Fact]
    public void Read_ConcatenatesOcrTextFromAllPages()
    {
        // Реальный PDF нужен только потому, что OcrPdfReader валидирует File.Exists.
        var pdfPath = GetTestFilePath("simple-text.pdf");
        Assert.True(File.Exists(pdfPath));

        var renderer = new FakeRenderer(new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 });
        var ocr = new StubOcr(call => $"стр {call + 1}");
        var reader = new OcrPdfReader(renderer, ocr);

        var result = reader.Read(pdfPath);

        Assert.Equal(3, result.PageCount);
        Assert.Equal(3, renderer.RenderCalls);
        Assert.Contains("стр 1", result.Text);
        Assert.Contains("стр 2", result.Text);
        Assert.Contains("стр 3", result.Text);
    }

    [Fact]
    public void Read_ScannedPdf_RecognizesEnglishMarkerEndToEnd()
    {
        // Полный пайплайн: PDFium рендерит → Tesseract распознаёт.
        var pdfPath = GetTestFilePath("scanned-text.pdf");
        Assert.True(File.Exists(pdfPath), $"Тестовый файл не найден: {pdfPath}");

        var reader = new OcrPdfReader(new PdfiumPdfRenderer(), _ocr);
        var result = reader.Read(pdfPath);

        Assert.Equal(1, result.PageCount);
        Assert.Contains("YASNOTEXT_TEST_MARKER", result.Text);
    }
}
