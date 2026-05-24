using System.Windows;

namespace YasnoText.UI;

/// <summary>
/// Простое модальное окно для ввода имени профиля. Возвращает
/// результат через <see cref="EnteredName"/> и <see cref="ShowDialog"/>.
/// При наличии forbiddenNames проверяет коллизии — диалог не закрывается,
/// пока пользователь не введёт уникальное имя или не нажмёт Отмена.
/// </summary>
public partial class ProfileNameDialog : Window
{
    private readonly HashSet<string> _forbiddenNames;

    public string EnteredName { get; private set; } = string.Empty;

    public ProfileNameDialog(
        string title,
        string prompt,
        string initialName,
        IEnumerable<string>? forbiddenNames = null)
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = prompt;
        NameTextBox.Text = initialName;

        _forbiddenNames = new HashSet<string>(
            forbiddenNames ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

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

        if (_forbiddenNames.Contains(value))
        {
            MessageBox.Show(
                $"Профиль с именем «{value}» уже существует. Выберите другое имя.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            NameTextBox.Focus();
            NameTextBox.SelectAll();
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
