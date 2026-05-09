using YasnoText.Core.Ocr;

namespace YasnoText.Core.DocumentReaders;

/// <summary>
/// Извлекает текст с растровых изображений через OCR.
/// Поддерживает png, jpg/jpeg, tif/tiff, bmp.
/// </summary>
public class OcrImageReader : IDocumentReader
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"
        };

    private readonly IOcrService _ocr;

    public OcrImageReader(IOcrService ocr)
    {
        _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
    }

    public bool CanRead(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }

    public DocumentResult Read(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Путь к файлу не может быть пустым.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Изображение не найдено: {filePath}", filePath);
        }

        var text = _ocr.Recognize(filePath);
        return new DocumentResult(text.TrimEnd(), 1);
    }
}
