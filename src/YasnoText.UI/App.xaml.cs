using System.Configuration;
using System.Data;
using System.Windows;
using YasnoText.UI.Themes;

namespace YasnoText.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Применяем стандартную тему до открытия окна,
        // чтобы у XAML-разметки сразу были все ресурсы.
        // Дальнейшие переключения тем выполняет MainViewModel.
        ThemeManager.ApplyTheme("Standard");
    }
}
