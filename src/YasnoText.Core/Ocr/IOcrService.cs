namespace YasnoText.Core.Ocr;

/// <summary>
/// Сервис распознавания текста на растровых изображениях.
/// Используется как для одиночных картинок (OcrImageReader),
/// так и для рендеренных страниц сканированных PDF.
/// </summary>
public interface IOcrService : IDisposable
{
    /// <summary>Распознать текст на изображении из файла.</summary>
    /// <exception cref="ArgumentException">Если путь пустой или null.</exception>
    /// <exception cref="FileNotFoundException">Если файл не существует.</exception>
    /// <exception cref="DirectoryNotFoundException">Если папка с языковыми моделями не найдена.</exception>
    string Recognize(string imagePath);

    /// <summary>Распознать текст на изображении, переданном как массив байтов.</summary>
    string Recognize(byte[] imageBytes);
}
