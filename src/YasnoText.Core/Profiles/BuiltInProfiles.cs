namespace YasnoText.Core.Profiles;

/// <summary>
/// Встроенные профили, доступные сразу после установки приложения.
/// Эти профили нельзя удалить, но пользователь может создать их копии
/// и настроить под себя.
/// </summary>
public static class BuiltInProfiles
{
    /// <summary>
    /// Стандартный профиль: чёрный текст на белом фоне, шрифт Segoe UI 14pt.
    /// Используется как стартовая точка для всех пользователей.
    /// </summary>
    public static ReadingProfile Standard => new ReadingProfile
    {
        Id = "standard",
        Name = "Стандартный",
        Description = "Базовый режим без адаптаций",
        FontFamily = "Segoe UI",
        FontSize = 14,
        IsBold = false,
        LetterSpacing = 0,
        LineHeight = 1.5,
        WordSpacing = 0,
        IsBuiltIn = true,
        BaseThemeId = "standard",
        Colors = new ColorScheme
        {
            Name = "Светлая",
            WindowBackground = "#FFFFFF",
            ReadingBackground = "#FFFFFF",
            TextColor = "#2C2C2A",
            AccentColor = "#1E88E5",
            BorderColor = "#D0D0C8"
        }
    };

    /// <summary>
    /// Профиль для слабовидящих: жёлтый текст на чёрном фоне (контраст 21:1),
    /// крупный жирный шрифт Arial 28pt. Соответствует требованиям WCAG AAA.
    /// </summary>
    public static ReadingProfile LowVision => new ReadingProfile
    {
        Id = "low-vision",
        Name = "Слабовидение",
        Description = "Максимальный контраст и крупный шрифт",
        FontFamily = "Arial",
        FontSize = 28,
        IsBold = true,
        LetterSpacing = 1,
        LineHeight = 1.7,
        WordSpacing = 2,
        IsBuiltIn = true,
        BaseThemeId = "low-vision",
        Colors = new ColorScheme
        {
            Name = "Контрастная",
            WindowBackground = "#000000",
            ReadingBackground = "#000000",
            TextColor = "#FFFF00",
            AccentColor = "#FFFF00",
            BorderColor = "#FFFF00"
        }
    };

    /// <summary>
    /// Профиль для людей с дислексией: специальный шрифт OpenDyslexic,
    /// расширенные интервалы, бежевый фон вместо белого (снижает «ослепляющий эффект»).
    /// </summary>
    public static ReadingProfile Dyslexia => new ReadingProfile
    {
        Id = "dyslexia",
        Name = "Дислексия",
        Description = "Шрифт OpenDyslexic и расширенные интервалы",
        FontFamily = "OpenDyslexic",
        FontSize = 18,
        IsBold = false,
        LetterSpacing = 1.5,
        LineHeight = 2.2,
        WordSpacing = 4,
        IsBuiltIn = true,
        BaseThemeId = "dyslexia",
        Colors = new ColorScheme
        {
            Name = "Бежевая",
            WindowBackground = "#E8D5B0",
            ReadingBackground = "#F4E8D0",
            TextColor = "#4A3520",
            AccentColor = "#6B4F2C",
            BorderColor = "#8B6F47"
        }
    };

    /// <summary>
    /// Возвращает все встроенные профили в порядке их отображения в UI.
    /// </summary>
    public static IReadOnlyList<ReadingProfile> All => new[]
    {
        Standard,
        LowVision,
        Dyslexia
    };
}
