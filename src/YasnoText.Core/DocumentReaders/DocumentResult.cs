namespace YasnoText.Core.DocumentReaders;

/// <summary>
/// Результат чтения документа: извлечённый текст и количество страниц.
/// </summary>
public class DocumentResult
{
    public DocumentResult(string text, int pageCount)
    {
        Text = text ?? string.Empty;
        PageCount = pageCount;
    }

    /// <summary>Извлечённый текст со всех страниц документа.</summary>
    public string Text { get; }

    /// <summary>Количество страниц в исходном документе.</summary>
    public int PageCount { get; }

    /// <summary>
    /// true, если в документе нет читаемого текста.
    /// Это сигнал для запуска OCR (на следующем коммите).
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);

    /// <summary>Возвращает пустой результат — для случаев, когда документ не загружен.</summary>
    public static DocumentResult Empty() => new DocumentResult(string.Empty, 0);
}
