using YasnoText.Core.DocumentReaders;

namespace YasnoText.Tests;

public class PdfTextOrOcrReaderTests
{
    private sealed class StubReader : IDocumentReader
    {
        private readonly DocumentResult _result;
        public bool ReadCalled { get; private set; }
        public StubReader(DocumentResult result) => _result = result;

        public bool CanRead(string filePath) => true;

        public DocumentResult Read(string filePath)
        {
            ReadCalled = true;
            return _result;
        }
    }

    [Fact]
    public void Read_TextReaderReturnsNonEmpty_DoesNotInvokeOcr()
    {
        var text = new StubReader(new DocumentResult("реальный текстовый слой", 5));
        var ocr = new StubReader(new DocumentResult("OCR результат", 5));
        var composite = new PdfTextOrOcrReader(text, ocr);

        var result = composite.Read("any.pdf");

        Assert.Equal("реальный текстовый слой", result.Text);
        Assert.Equal(5, result.PageCount);
        Assert.True(text.ReadCalled);
        Assert.False(ocr.ReadCalled);
    }

    [Fact]
    public void Read_TextReaderReturnsEmpty_FallsBackToOcr()
    {
        var text = new StubReader(DocumentResult.Empty());
        var ocr = new StubReader(new DocumentResult("текст из OCR", 3));
        var composite = new PdfTextOrOcrReader(text, ocr);

        var result = composite.Read("any.pdf");

        Assert.Equal("текст из OCR", result.Text);
        Assert.Equal(3, result.PageCount);
        Assert.True(text.ReadCalled);
        Assert.True(ocr.ReadCalled);
    }

    [Fact]
    public void CanRead_DelegatesToTextReader()
    {
        var text = new StubReader(DocumentResult.Empty());
        var ocr = new StubReader(DocumentResult.Empty());
        var composite = new PdfTextOrOcrReader(text, ocr);

        // StubReader.CanRead всегда true — проверяем, что композит просто
        // делегирует, а не добавляет собственную логику.
        Assert.True(composite.CanRead("file.pdf"));
        Assert.True(composite.CanRead("file.txt"));
    }

    [Fact]
    public void Ctor_NullArguments_ThrowArgumentNullException()
    {
        var text = new StubReader(DocumentResult.Empty());

        Assert.Throws<ArgumentNullException>(
            () => new PdfTextOrOcrReader(null!, text));
        Assert.Throws<ArgumentNullException>(
            () => new PdfTextOrOcrReader(text, null!));
    }
}
