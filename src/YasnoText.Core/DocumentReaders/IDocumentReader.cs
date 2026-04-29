namespace YasnoText.Core.DocumentReaders;

/// <summary>
/// Универсальный интерфейс для чтения документов разных форматов.
/// Реализации: PdfTextReader, DocxReader (в будущем) и т.д.
/// </summary>
public interface IDocumentReader
{
    /// <summary>
    /// Может ли этот ридер прочитать файл по указанному пути.
    /// Проверка обычно по расширению.
    /// </summary>
    bool CanRead(string filePath);

    /// <summary>
    /// Прочитать файл и вернуть извлечённый текст.
    /// </summary>
    /// <exception cref="ArgumentException">Если путь пустой или null.</exception>
    /// <exception cref="FileNotFoundException">Если файл не существует.</exception>
    DocumentResult Read(string filePath);
}
