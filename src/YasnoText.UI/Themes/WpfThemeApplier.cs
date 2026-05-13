using YasnoText.Core.Profiles;

namespace YasnoText.UI.Themes;

/// <summary>
/// Реализация IThemeApplier, использующая WPF-словари ресурсов.
/// Преобразует идентификатор профиля в имя файла темы и вызывает ThemeManager.
/// </summary>
public class WpfThemeApplier : IThemeApplier
{
    public void Apply(string themeId)
    {
        var themeName = themeId switch
        {
            "standard" => "Standard",
            "low-vision" => "LowVision",
            "dyslexia" => "Dyslexia",
            _ => "Standard"
        };

        ThemeManager.ApplyTheme(themeName);
    }
}
