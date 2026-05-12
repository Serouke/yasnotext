using System.Windows;

namespace YasnoText.UI;

/// <summary>
/// Простое модальное окно для ввода имени профиля. Возвращает
/// результат через <see cref="EnteredName"/> и <see cref="ShowDialog"/>.
/// </summary>
public partial class ProfileNameDialog : Window
{
    public string EnteredName { get; private set; } = string.Empty;

    public ProfileNameDialog(string title, string prompt, string initialName)
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        NameTextBox.Text = initialName;

        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var value = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            MessageBox.Show(
                "Имя профиля не может быть пустым.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        EnteredName = value;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
