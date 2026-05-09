namespace YasnoText.Core.DocumentReaders;

/// <summary>
/// Композитный PDF-ридер: сначала пробует извлечь текстовый слой
/// (PdfTextReader), если он пустой — падает в OCR (OcrPdfReader).
/// Внешне выглядит как обычный IDocumentReader для .pdf, поэтому
/// MainViewModel ничего не знает про fallback.
/// </summary>
public class PdfTextOrOcrReader : IDocumentReader
{
    private readonly IDocumentReader _textReader;
    private readonly IDocumentReader _ocrReader;

    public PdfTextOrOcrReader(IDocumentReader textReader, IDocumentReader ocrReader)
    {
        _textReader = textReader ?? throw new ArgumentNullException(nameof(textReader));
        _ocrReader = ocrReader ?? throw new ArgumentNullException(nameof(ocrReader));
    }

    public bool CanRead(string filePath) => _textReader.CanRead(filePath);

    public DocumentResult Read(string filePath)
    {
        var textResult = _textReader.Read(filePath);
        if (!textResult.IsEmpty)
        {
            return textResult;
        }

        // Текстового слоя нет — почти наверняка скан. Пробуем OCR.
        return _ocrReader.Read(filePath);
    }
}
