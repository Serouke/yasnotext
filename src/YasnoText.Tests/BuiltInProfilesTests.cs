using YasnoText.Core.Profiles;

namespace YasnoText.Tests;

public class BuiltInProfilesTests
{
    [Fact]
    public void All_ContainsThreeBuiltInProfiles()
    {
        // Проверяем, что у нас ровно три встроенных профиля.
        var profiles = BuiltInProfiles.All;

        Assert.Equal(3, profiles.Count);
    }

    [Fact]
    public void All_ProfilesHaveUniqueIds()
    {
        // У каждого профиля должен быть уникальный идентификатор —
        // иначе их нельзя будет различить в коде.
        var profiles = BuiltInProfiles.All;
        var uniqueIds = profiles.Select(p => p.Id).Distinct().Count();

        Assert.Equal(profiles.Count, uniqueIds);
    }

    [Fact]
    public void All_ProfilesAreMarkedAsBuiltIn()
    {
        // Все три встроенных профиля должны иметь флаг IsBuiltIn = true.
        var profiles = BuiltInProfiles.All;

        Assert.All(profiles, p => Assert.True(p.IsBuiltIn));
    }

    [Fact]
    public void Standard_HasReasonableDefaults()
    {
        var profile = BuiltInProfiles.Standard;

        Assert.Equal("Стандартный", profile.Name);
        Assert.Equal("Segoe UI", profile.FontFamily);
        Assert.Equal(14, profile.FontSize);
        Assert.False(profile.IsBold);
    }

    [Fact]
    public void LowVision_HasHighContrastColors()
    {
        // Профиль слабовидения должен использовать чёрный фон и жёлтый текст.
        var profile = BuiltInProfiles.LowVision;

        Assert.Equal("#000000", profile.Colors.WindowBackground);
        Assert.Equal("#FFFF00", profile.Colors.TextColor);
    }

    [Fact]
    public void LowVision_HasLargeFontSize()
    {
        // Размер шрифта должен быть значительно больше стандартного.
        var profile = BuiltInProfiles.LowVision;

        Assert.True(profile.FontSize >= 24,
            $"Размер шрифта {profile.FontSize} слишком мал для слабовидящих");
    }

    [Fact]
    public void Dyslexia_UsesOpenDyslexicFont()
    {
        var profile = BuiltInProfiles.Dyslexia;

        Assert.Equal("OpenDyslexic", profile.FontFamily);
    }

    [Fact]
    public void All_BuiltInProfiles_HaveBaseThemeIdMatchingTheirId()
    {
        // У встроенных профилей BaseThemeId совпадает с Id —
        // именно их Id используется как имена тем (StandardTheme.xaml и т.д.).
        var profiles = BuiltInProfiles.All;

        Assert.All(profiles, p =>
            Assert.Equal(p.Id, p.BaseThemeId));
    }

    [Fact]
    public void Dyslexia_HasIncreasedSpacing()
    {
        // Увеличенные интервалы — ключевая особенность профиля для дислексии.
        var profile = BuiltInProfiles.Dyslexia;

        Assert.True(profile.LetterSpacing > 0, "Должен быть увеличен межбуквенный интервал");
        Assert.True(profile.WordSpacing > 0, "Должен быть увеличен межсловный интервал");
        Assert.True(profile.LineHeight >= 2.0, "Высота строки должна быть не меньше 2.0");
    }
}
