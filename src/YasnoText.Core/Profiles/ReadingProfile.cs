namespace YasnoText.Core.Profiles;

/// <summary>
/// Профиль чтения — набор настроек, который применяется ко всему приложению
/// в один клик. Каждый профиль описывает шрифт, размер, интервалы и цвета
/// под конкретные потребности пользователя.
/// </summary>
public class ReadingProfile
{
    /// <summary>Уникальный идентификатор профиля (для встроенных — "standard", "low-vision" и т.п.).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Название профиля для отображения в UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Краткое описание, кому подходит профиль.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Имя шрифта (например, "Segoe UI", "Arial", "OpenDyslexic").</summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>Размер шрифта в пунктах.</summary>
    public double FontSize { get; set; } = 14;

    /// <summary>Жирность шрифта: false = обычный, true = жирный.</summary>
    public bool IsBold { get; set; } = false;

    /// <summary>Дополнительный межбуквенный интервал в пикселях (0 = стандартный).</summary>
    public double LetterSpacing { get; set; } = 0;

    /// <summary>Высота строки как множитель размера шрифта (1.5 = полуторный интервал).</summary>
    public double LineHeight { get; set; } = 1.5;

    /// <summary>Дополнительный межсловный интервал в пикселях.</summary>
    public double WordSpacing { get; set; } = 0;

    /// <summary>Цветовая схема профиля.</summary>
    public ColorScheme Colors { get; set; } = new ColorScheme();

    /// <summary>true — встроенный профиль (нельзя удалить), false — пользовательский.</summary>
    public bool IsBuiltIn { get; set; } = false;

    /// <summary>
    /// Идентификатор темы оформления (цветовой схемы), которая применяется при
    /// активации профиля: "standard", "low-vision" или "dyslexia". Для встроенных
    /// совпадает с Id; пользовательский профиль наследует значение от того
    /// встроенного, на основе которого он был создан.
    /// </summary>
    public string BaseThemeId { get; set; } = "standard";
}
