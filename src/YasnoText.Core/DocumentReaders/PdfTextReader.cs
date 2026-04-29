using System.Text;
using UglyToad.PdfPig;

namespace YasnoText.Core.DocumentReaders;

/// <summary>
/// Извлекает текст из PDF-файлов с помощью библиотеки PdfPig.
/// Работает с PDF, у которых есть текстовый слой. Сканы (растровые PDF)
/// вернутся с пустым текстом — для них нужен OCR.
/// </summary>
public class PdfTextReader : IDocumentReader
{
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
            throw new FileNotFoundException($"PDF-файл не найден: {filePath}", filePath);
        }

        using var document = PdfDocument.Open(filePath);
        var pageCount = document.NumberOfPages;
        var allText = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                allText.AppendLine(pageText);
                allText.AppendLine(); // пустая строка между страницами
            }
        }

        return new DocumentResult(allText.ToString().TrimEnd(), pageCount);
    }
}
