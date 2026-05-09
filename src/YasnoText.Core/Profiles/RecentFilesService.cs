using System.Text.Json;

namespace YasnoText.Core.Profiles;

/// <summary>
/// Хранит список последних открытых документов в JSON.
/// Хранилище: %APPDATA%/YasnoText/recent.json
/// Поведение LRU: при повторном открытии файл переезжает наверх,
/// список ограничен MaxItems элементами.
/// </summary>
public class RecentFilesService
{
    public const int MaxItems = 10;

    private readonly string _storageDirectory;
    private readonly string _storagePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public RecentFilesService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storageDirectory = Path.Combine(appData, "YasnoText");
        _storagePath = Path.Combine(_storageDirectory, "recent.json");
    }

    public RecentFilesService(string storageDirectory)
    {
        _storageDirectory = storageDirectory;
        _storagePath = Path.Combine(storageDirectory, "recent.json");
    }

    /// <summary>Возвращает список путей в порядке от самого свежего к самому старому.</summary>
    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(_storagePath))
        {
            return Array.Empty<string>();
        }

        try
        {
            var json = File.ReadAllText(_storagePath);
            var paths = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return paths ?? new List<string>();
        }
        catch (JsonException)
        {
            // Файл повреждён — игнорируем, считаем что списка нет.
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Добавляет файл в начало списка. Если файл уже есть — он перемещается
    /// наверх, дубликат удаляется. Список усечётся до MaxItems элементов.
    /// </summary>
    public void Add(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Путь к файлу не может быть пустым.", nameof(filePath));
        }

        var current = Load().ToList();

        // LRU: удаляем существующее вхождение (case-insensitive — Windows-пути).
        current.RemoveAll(p =>
            string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase));

        current.Insert(0, filePath);

        if (current.Count > MaxItems)
        {
            current.RemoveRange(MaxItems, current.Count - MaxItems);
        }

        Save(current);
    }

    /// <summary>Удаляет один путь из списка (например, если файл больше не существует).</summary>
    public void Remove(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var current = Load().ToList();
        var removed = current.RemoveAll(p =>
            string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            Save(current);
        }
    }

    private void Save(List<string> items)
    {
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }

        var json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(_storagePath, json);
    }
}
