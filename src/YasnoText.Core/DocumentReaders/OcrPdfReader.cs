using System.Text;
using YasnoText.Core.Ocr;

namespace YasnoText.Core.DocumentReaders;

/// <summary>
/// Извлекает текст из PDF через OCR: рендерит каждую страницу в bitmap
/// и прогоняет через Tesseract. Применяется для сканов без текстового слоя —
/// обычно через композитный PdfTextOrOcrReader, не напрямую.
/// </summary>
public class OcrPdfReader : IDocumentReader
{
    private readonly IPdfRenderer _renderer;
    private readonly IOcrService _ocr;
    private readonly int _dpi;

    public OcrPdfReader(IPdfRenderer renderer, IOcrService ocr, int dpi = 300)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        _dpi = dpi;
    }

    public bool CanRead(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return Path.GetExtension(filePath)
            .Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public DocumentResult Read(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Путь к файлу не может быть пустым.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"PDF не найден: {filePath}", filePath);
        }

        var pageCount = _renderer.GetPageCount(filePath);
        var sb = new StringBuilder();

        for (int i = 0; i < pageCount; i++)
        {
            var pngBytes = _renderer.RenderPageAsPng(filePath, i, _dpi);
            var text = _ocr.Recognize(pngBytes);
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
                sb.AppendLine();
            }
        }

        return new DocumentResult(sb.ToString().TrimEnd(), pageCount);
    }
}
