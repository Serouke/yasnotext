using System.Text.Json;

namespace YasnoText.Core.Profiles;

/// <summary>
/// Управляет загрузкой и сохранением пользовательских профилей в JSON.
/// Хранилище: %APPDATA%/YasnoText/profiles.json
///
/// Встроенные профили (BuiltInProfiles) всегда доступны и не сохраняются на диск —
/// они генерируются программно. Сохраняются только пользовательские настройки.
/// </summary>
public class ProfileManager
{
    private readonly string _storageDirectory;
    private readonly string _storagePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Создаёт менеджер с путём по умолчанию: %APPDATA%/YasnoText/.
    /// </summary>
    public ProfileManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storageDirectory = Path.Combine(appData, "YasnoText");
        _storagePath = Path.Combine(_storageDirectory, "profiles.json");
    }

    /// <summary>
    /// Создаёт менеджер с произвольным путём (используется в тестах).
    /// </summary>
    public ProfileManager(string storageDirectory)
    {
        _storageDirectory = storageDirectory;
        _storagePath = Path.Combine(storageDirectory, "profiles.json");
    }

    /// <summary>
    /// Возвращает все доступные профили: встроенные + сохранённые пользовательские.
    /// </summary>
    public IReadOnlyList<ReadingProfile> LoadAll()
    {
        var profiles = new List<ReadingProfile>(BuiltInProfiles.All);

        if (File.Exists(_storagePath))
        {
            try
            {
                var json = File.ReadAllText(_storagePath);
                var userProfiles = JsonSerializer.Deserialize<List<ReadingProfile>>(json, JsonOptions);
                if (userProfiles != null)
                {
                    profiles.AddRange(userProfiles);
                }
            }
            catch (JsonException)
            {
                // Файл повреждён — игнорируем, используем только встроенные профили.
                // В реальном приложении здесь стоило бы залогировать ошибку.
            }
        }

        return profiles;
    }

    /// <summary>
    /// Сохраняет список пользовательских профилей в JSON.
    /// Встроенные профили исключаются — они не нуждаются в сохранении.
    /// </summary>
    public void SaveUserProfiles(IEnumerable<ReadingProfile> profiles)
    {
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }

        var userProfiles = profiles.Where(p => !p.IsBuiltIn).ToList();
        var json = JsonSerializer.Serialize(userProfiles, JsonOptions);
        File.WriteAllText(_storagePath, json);
    }
}
