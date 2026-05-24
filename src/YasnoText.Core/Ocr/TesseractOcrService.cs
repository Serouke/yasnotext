using Tesseract;

namespace YasnoText.Core.Ocr;

/// <summary>
/// Реализация IOcrService на базе нативного Tesseract.
/// Engine инициализируется лениво при первом распознавании, чтобы
/// отсутствующие языковые модели не валили приложение на старте.
/// </summary>
/// <remarks>
/// TesseractEngine не потокобезопасен, поэтому каждый Recognize
/// сериализуется через lock. Для MVP этого достаточно — параллельный
/// OCR для нескольких страниц можно будет добавить через пул engine'ов.
/// </remarks>
public sealed class TesseractOcrService : IOcrService
{
    private readonly string _tessdataPath;
    private readonly string _languages;
    private readonly Lock _gate = new();
    private TesseractEngine? _engine;
    private bool _disposed;

    public TesseractOcrService(string tessdataPath, string languages = "eng+rus")
    {
        if (string.IsNullOrWhiteSpace(tessdataPath))
        {
            throw new ArgumentException("Путь к tessdata не может быть пустым.", nameof(tessdataPath));
        }

        if (string.IsNullOrWhiteSpace(languages))
        {
            throw new ArgumentException("Список языков не может быть пустым.", nameof(languages));
        }

        _tessdataPath = tessdataPath;
        _languages = languages;
    }

    public string Recognize(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Путь к файлу не может быть пустым.", nameof(imagePath));
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"Изображение не найдено: {imagePath}", imagePath);
        }

        lock (_gate)
        {
            EnsureEngine();
            using var img = Pix.LoadFromFile(imagePath);
            using var page = _engine!.Process(img);
            return page.GetText() ?? string.Empty;
        }
    }

    public string Recognize(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            throw new ArgumentException("Массив байтов изображения пуст.", nameof(imageBytes));
        }

        lock (_gate)
        {
            EnsureEngine();
            using var img = Pix.LoadFromMemory(imageBytes);
            using var page = _engine!.Process(img);
            return page.GetText() ?? string.Empty;
        }
    }

    private void EnsureEngine()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_engine != null)
        {
            return;
        }

        if (!Directory.Exists(_tessdataPath))
        {
            throw new DirectoryNotFoundException(
                $"Папка с языковыми моделями Tesseract не найдена: {_tessdataPath}. " +
                $"Скачайте eng.traineddata и rus.traineddata из tessdata_fast.");
        }

        _engine = new TesseractEngine(_tessdataPath, _languages, EngineMode.Default);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _engine?.Dispose();
        _engine = null;
        _disposed = true;
    }
}
