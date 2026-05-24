using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace YasnoText.Core.DocumentReaders;

/// <summary>
/// Извлекает текст из DOCX-файлов через DocumentFormat.OpenXml.
/// Бинарный .doc (Word 97–2003) не поддерживается — для него нужен другой парсер.
/// </summary>
public class DocxTextReader : IDocumentReader
{
    public bool CanRead(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return Path.GetExtension(filePath)
            .Equals(".docx", StringComparison.OrdinalIgnoreCase);
    }

    public DocumentResult Read(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Путь к файлу не может быть пустым.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"DOCX-файл не найден: {filePath}", filePath);
        }

        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            return new DocumentResult(string.Empty, 1);
        }

        var sb = new StringBuilder();
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
        }

        // Точное число страниц в .docx без рендера Word недоступно.
        // Считаем по явным page-break-ам — для документов, прошедших через Word,
        // также сработает w:lastRenderedPageBreak.
        var pageBreaks = body.Descendants<Break>()
            .Count(b => b.Type != null && b.Type == BreakValues.Page);
        var lastRenderedBreaks = body.Descendants<LastRenderedPageBreak>().Count();
        var pageCount = 1 + Math.Max(pageBreaks, lastRenderedBreaks);

        return new DocumentResult(sb.ToString().TrimEnd(), pageCount);
    }
}
