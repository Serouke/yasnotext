namespace YasnoText.Core.Profiles;

/// <summary>
/// Цветовая схема, описывающая основные цвета интерфейса и области чтения.
/// Цвета хранятся в формате HEX (например, "#FFFFFF").
/// </summary>
public class ColorScheme
{
    /// <summary>Название схемы для отображения пользователю.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Цвет фона главного окна.</summary>
    public string WindowBackground { get; set; } = "#FFFFFF";

    /// <summary>Цвет фона области чтения.</summary>
    public string ReadingBackground { get; set; } = "#FFFFFF";

    /// <summary>Цвет основного текста.</summary>
    public string TextColor { get; set; } = "#000000";

    /// <summary>Цвет акцентов: активный профиль, главные кнопки.</summary>
    public string AccentColor { get; set; } = "#1E88E5";

    /// <summary>Цвет границ и разделителей.</summary>
    public string BorderColor { get; set; } = "#D0D0C8";
}
