using System;
using System.Windows;

namespace YasnoText.UI.Themes;

/// <summary>
/// Управляет переключением тем оформления приложения.
/// Темы хранятся в виде ResourceDictionary в папке Themes/.
/// При смене темы все элементы UI, использующие DynamicResource,
/// автоматически перерисовываются.
/// </summary>
public static class ThemeManager
{
    private const string AssemblyName = "YasnoText.UI";

    /// <summary>
    /// Применяет тему к приложению. Все предыдущие темы удаляются.
    /// </summary>
    /// <param name="themeName">Имя темы: "Standard", "LowVision" или "Dyslexia".</param>
    /// <exception cref="ArgumentException">Если передано неизвестное имя темы.</exception>
    public static void ApplyTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            throw new ArgumentException("Имя темы не может быть пустым.", nameof(themeName));
        }

        if (themeName != "Standard" && themeName != "LowVision" && themeName != "Dyslexia")
        {
            throw new ArgumentException(
                $"Неизвестная тема: '{themeName}'. Допустимые значения: Standard, LowVision, Dyslexia.",
                nameof(themeName));
        }

        var themeUri = new Uri(
            $"/{AssemblyName};component/Themes/{themeName}Theme.xaml",
            UriKind.Relative);

        var theme = (ResourceDictionary)Application.LoadComponent(themeUri);

        // Очищаем предыдущие темы и применяем новую.
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(theme);
    }
}
