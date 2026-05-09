using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using YasnoText.UI.Themes;
using YasnoText.UI.ViewModels;

namespace YasnoText.UI;

public partial class MainWindow : Window
{
    private static readonly string[] ParagraphSeparators = { "\r\n\r\n", "\n\n" };

    /// <summary>Радиус «мёртвой зоны» вокруг якоря, в пикселях. Внутри — скролл стоит.</summary>
    private const double AutoScrollDeadZone = 12;

    /// <summary>Множитель скорости: пиксели прокрутки за тик на пиксель смещения курсора.</summary>
    private const double AutoScrollSpeedFactor = 0.06;

    private readonly MainViewModel _viewModel;

    private bool _autoScrollActive;
    private Point _autoScrollAnchor;
    private DispatcherTimer? _autoScrollTimer;
    private ScrollViewer? _innerScrollViewer;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(new WpfThemeApplier());
        DataContext = _viewModel;

        // FlowDocument строится в code-behind, потому что у FlowDocument
        // нет ItemsSource или биндинга к строке — нужен явный пересбор
        // блоков при изменении текста или межстрочного интервала.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateReadingDocument();

        // Автопрокрутка. Middle-click ловим через PreviewMouseDown viewer'а;
        // выход (любой другой клик / Esc) — глобально на окне.
        ReadingViewer.PreviewMouseDown += OnReadingViewerPreviewMouseDown;
        PreviewMouseDown += OnWindowPreviewMouseDownForAutoScroll;
        PreviewKeyDown += OnWindowPreviewKeyDownForAutoScroll;

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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.DocumentText) ||
            e.PropertyName == nameof(MainViewModel.EffectiveLineHeight))
        {
            UpdateReadingDocument();
            // При загрузке нового документа автопрокрутка — лишний сюрприз.
            StopAutoScroll();
        }
    }

    /// <summary>
    /// Пересобирает FlowDocument из текущего DocumentText, разбивая текст на
    /// параграфы по двойному переносу строки. Виртуализация WPF работает
    /// на уровне Paragraph-блоков, поэтому один большой Run на 250-страничный
    /// документ не сработал бы — скролл всё равно лагал бы.
    /// </summary>
    private void UpdateReadingDocument()
    {
        var text = _viewModel.DocumentText ?? string.Empty;

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40, 30, 40, 30),
            LineHeight = _viewModel.EffectiveLineHeight,
            // FontFamily/FontSize/Foreground наследуются от FlowDocumentScrollViewer
            // через TextElement-наследование, поэтому здесь их явно не задаём.
        };

        var paragraphs = text.Split(ParagraphSeparators, StringSplitOptions.None);
        foreach (var paragraphText in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraphText))
            {
                continue;
            }

            doc.Blocks.Add(new Paragraph(new Run(paragraphText)));
        }

        ReadingViewer.Document = doc;
    }

    private void OnAutoScrollButtonClick(object sender, RoutedEventArgs e)
    {
        if (_autoScrollActive)
        {
            StopAutoScroll();
            return;
        }

        if (!_viewModel.HasDocument)
        {
            return;
        }

        // Стартуем с центра области чтения.
        var center = new Point(
            AutoScrollOverlay.ActualWidth / 2,
            AutoScrollOverlay.ActualHeight / 2);
        StartAutoScroll(center);
    }

    private void OnReadingViewerPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        if (!_viewModel.HasDocument)
        {
            return;
        }

        if (_autoScrollActive)
        {
            StopAutoScroll();
        }
        else
        {
            StartAutoScroll(e.GetPosition(AutoScrollOverlay));
        }

        // Иначе FlowDocumentScrollViewer попробует сделать свой autoscroll/pan.
        e.Handled = true;
    }

    private void OnWindowPreviewMouseDownForAutoScroll(object sender, MouseButtonEventArgs e)
    {
        // Любой клик кроме middle при активном режиме — выход.
        // Middle на самом viewer'е обработан выше.
        if (_autoScrollActive && e.ChangedButton != MouseButton.Middle)
        {
            StopAutoScroll();
        }
    }

    private void OnWindowPreviewKeyDownForAutoScroll(object sender, KeyEventArgs e)
    {
        if (_autoScrollActive && e.Key == Key.Escape)
        {
            StopAutoScroll();
            e.Handled = true;
        }
    }

    private void StartAutoScroll(Point anchor)
    {
        EnsureInnerScrollViewer();
        if (_innerScrollViewer == null)
        {
            return;
        }

        _autoScrollAnchor = anchor;
        _autoScrollActive = true;

        Canvas.SetLeft(AutoScrollAnchor, anchor.X - AutoScrollAnchor.Width / 2);
        Canvas.SetTop(AutoScrollAnchor, anchor.Y - AutoScrollAnchor.Height / 2);
        AutoScrollOverlay.Visibility = Visibility.Visible;
        Cursor = Cursors.ScrollAll;

        _autoScrollTimer ??= new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _autoScrollTimer.Tick -= OnAutoScrollTick;
        _autoScrollTimer.Tick += OnAutoScrollTick;
        _autoScrollTimer.Start();
    }

    private void StopAutoScroll()
    {
        if (!_autoScrollActive)
        {
            return;
        }

        _autoScrollActive = false;
        _autoScrollTimer?.Stop();
        AutoScrollOverlay.Visibility = Visibility.Collapsed;
        ClearValue(CursorProperty);
    }

    private void OnAutoScrollTick(object? sender, EventArgs e)
    {
        if (!_autoScrollActive || _innerScrollViewer == null)
        {
            return;
        }

        var current = Mouse.GetPosition(AutoScrollOverlay);
        var delta = current.Y - _autoScrollAnchor.Y;
        var absDelta = Math.Abs(delta);

        if (absDelta < AutoScrollDeadZone)
        {
            // В мёртвой зоне курсор «нейтральный» — стоим на месте.
            Cursor = Cursors.ScrollAll;
            return;
        }

        var direction = Math.Sign(delta);
        var speed = direction * (absDelta - AutoScrollDeadZone) * AutoScrollSpeedFactor;

        _innerScrollViewer.ScrollToVerticalOffset(
            _innerScrollViewer.VerticalOffset + speed);

        Cursor = direction > 0 ? Cursors.ScrollS : Cursors.ScrollN;
    }

    private void EnsureInnerScrollViewer()
    {
        if (_innerScrollViewer != null)
        {
            return;
        }

        // FlowDocumentScrollViewer держит фактический ScrollViewer в template'е
        // под именем PART_ContentHost. До ApplyTemplate() он может быть null.
        ReadingViewer.ApplyTemplate();
        _innerScrollViewer = ReadingViewer.Template?
            .FindName("PART_ContentHost", ReadingViewer) as ScrollViewer;
    }
}
