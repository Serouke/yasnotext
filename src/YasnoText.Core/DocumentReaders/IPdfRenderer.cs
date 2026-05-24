namespace YasnoText.Core.DocumentReaders;

/// <summary>
/// Рендерит страницы PDF в растровое изображение (PNG-байты).
/// Нужен для OCR сканированных PDF, у которых нет текстового слоя.
/// </summary>
public interface IPdfRenderer
{
    /// <summary>Количество страниц в PDF.</summary>
    int GetPageCount(string pdfPath);

    /// <summary>Отрендерить одну страницу (0-based) и вернуть PNG-байты.</summary>
    byte[] RenderPageAsPng(string pdfPath, int pageIndex, int dpi = 300);
}
