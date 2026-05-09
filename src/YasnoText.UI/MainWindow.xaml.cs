using System.Windows;
using System.Windows.Input;
using YasnoText.UI.Themes;
using YasnoText.UI.ViewModels;

namespace YasnoText.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(new WpfThemeApplier());
        DataContext = _viewModel;

        // Drag-and-drop файла из проводника. Сама подписка через AllowDrop
        // и атрибуты DragOver/Drop в XAML — здесь только обработчики.

        // Горячие клавиши переключения профиля
        InputBindings.Add(new KeyBinding(
            new RelayCommand(() => _viewModel.ActivateById("standard")),
            Key.D1, ModifierKeys.Control));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(() => _viewModel.ActivateById("low-vision")),
            Key.D2, ModifierKeys.Control));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(() => _viewModel.ActivateById("dyslexia")),
            Key.D3, ModifierKeys.Control));

        // Горячая клавиша открытия документа
        InputBindings.Add(new KeyBinding(
            _viewModel.OpenDocumentCommand,
            Key.O, ModifierKeys.Control));

        // Сохранить текущие настройки как пользовательский профиль.
        InputBindings.Add(new KeyBinding(
            _viewModel.SaveProfileCommand,
            Key.S, ModifierKeys.Control));

        // Изменение размера шрифта. OemPlus/OemMinus — это «=/+» и «-» на
        // основной части клавиатуры, Add/Subtract — на numpad.
        InputBindings.Add(new KeyBinding(
            _viewModel.IncreaseFontCommand,
            Key.OemPlus, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(
            _viewModel.IncreaseFontCommand,
            Key.Add, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(
            _viewModel.DecreaseFontCommand,
            Key.OemMinus, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(
            _viewModel.DecreaseFontCommand,
            Key.Subtract, ModifierKeys.Control));
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        // Курсор меняется на «копировать» только если перетаскивается файл.
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length == 0)
        {
            return;
        }

        // Если бросили несколько файлов — открываем первый. UI на нескольких
        // вкладках/документах пока не рассчитан.
        await _viewModel.OpenFromPathAsync(files[0]);
    }
}
