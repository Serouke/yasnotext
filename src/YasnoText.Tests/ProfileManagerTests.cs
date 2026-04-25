using YasnoText.Core.Profiles;

namespace YasnoText.Tests;

public class ProfileManagerTests : IDisposable
{
    private readonly string _testDirectory;

    public ProfileManagerTests()
    {
        // Каждый тест работает в своей временной папке,
        // чтобы тесты не мешали друг другу.
        _testDirectory = Path.Combine(Path.GetTempPath(), $"YasnoTextTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // После каждого теста удаляем временную папку.
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadAll_WithNoStorageFile_ReturnsBuiltInProfiles()
    {
        // Если файл с пользовательскими профилями ещё не создан —
        // возвращаются только встроенные.
        var manager = new ProfileManager(_testDirectory);

        var profiles = manager.LoadAll();

        Assert.Equal(3, profiles.Count);
        Assert.All(profiles, p => Assert.True(p.IsBuiltIn));
    }

    [Fact]
    public void SaveUserProfiles_PersistsCustomProfile()
    {
        // Сохраняем пользовательский профиль и проверяем,
        // что после загрузки он есть в списке.
        var manager = new ProfileManager(_testDirectory);
        var customProfile = new ReadingProfile
        {
            Id = "my-custom",
            Name = "Мой профиль",
            FontFamily = "Verdana",
            FontSize = 16,
            IsBuiltIn = false
        };

        manager.SaveUserProfiles(new[] { customProfile });
        var loaded = manager.LoadAll();

        Assert.Equal(4, loaded.Count); // 3 встроенных + 1 свой
        Assert.Contains(loaded, p => p.Id == "my-custom" && p.Name == "Мой профиль");
    }

    [Fact]
    public void SaveUserProfiles_DoesNotPersistBuiltInProfiles()
    {
        // Встроенные профили не должны попадать в файл —
        // они генерируются программно при каждой загрузке.
        var manager = new ProfileManager(_testDirectory);
        var allProfiles = BuiltInProfiles.All.ToList();

        manager.SaveUserProfiles(allProfiles);
        var loaded = manager.LoadAll();

        // Должно быть ровно 3 (встроенные), а не 6 (если бы они задвоились).
        Assert.Equal(3, loaded.Count);
    }
}
