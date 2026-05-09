using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using YasnoText.UI.Themes;
using YasnoText.UI.ViewModels;

namespace YasnoText.UI;

public partial class MainWindow : Window
{
    private static readonly string[] ParagraphSeparators = { "\r\n\r\n", "\n\n" };

    /// <summary>Радиус «мёртвой зоны» вокруг якоря, в пикселях. Внутри — скролл стоит.</summary>
    private const double AutoScrollDeadZone = 12;

    /// <summary>Множитель квадратичной кривой скорости: target_speed = sign × (|delta|−dz)² × factor (px/s).</summary>
    private const double AutoScrollSpeedFactor = 0.05;

    /// <summary>Скорость сглаживания: насколько быстро текущая скорость подтягивается к целевой (1/сек).
    /// Чем больше — тем отзывчивее, но резче. 12 даёт ~0.4 сек до ~95% целевой скорости.</summary>
    private const double AutoScrollSmoothingRate = 12.0;

    private readonly MainViewModel _viewModel;

    private bool _autoScrollActive;
    private Point _autoScrollAnchor;
    private double _autoScrollCurrentSpeed;
    private TimeSpan _autoScrollLastFrameTime;
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
        // Любое изменение, влияющее на содержимое или внешний вид FlowDocument,
        // требует его пересборки — наследование TextElement-свойств от
        // FlowDocumentScrollViewer на свежесозданный FlowDocument в WPF
        // отрабатывает ненадёжно, поэтому проще пересоздавать целиком.
        if (e.PropertyName == nameof(MainViewModel.DocumentText) ||
            e.PropertyName == nameof(MainViewModel.EffectiveLineHeight) ||
            e.PropertyName == nameof(MainViewModel.CurrentFontFamily) ||
            e.PropertyName == nameof(MainViewModel.CurrentFontSize))
        {
            UpdateReadingDocument();
        }

        if (e.PropertyName == nameof(MainViewModel.DocumentText))
        {
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

        var fontFamily = new System.Windows.Media.FontFamily(_viewModel.CurrentFontFamily);
        var fontSize = _viewModel.CurrentFontSize;

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40, 30, 40, 30),
            FontFamily = fontFamily,
            FontSize = fontSize,
            LineHeight = _viewModel.EffectiveLineHeight,
        };

        var paragraphs = text.Split(ParagraphSeparators, StringSplitOptions.None);
        foreach (var paragraphText in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraphText))
            {
                continue;
            }

            // FontSize/FontFamily задаются и на FlowDocument, и на Paragraph:
            // в WPF наследование TextElement-свойств от программно созданного
            // FlowDocument к его блокам срабатывает не во всех конфигурациях
            // FlowDocumentScrollViewer (A+/A− меняли только LineHeight, шрифт
            // оставался прежним). Локальная установка на Paragraph — гарантия.
            doc.Blocks.Add(new Paragraph(new Run(paragraphText))
            {
                FontFamily = fontFamily,
                FontSize = fontSize,
            });
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
        _autoScrollCurrentSpeed = 0;
        _autoScrollLastFrameTime = TimeSpan.Zero;

        Canvas.SetLeft(AutoScrollAnchor, anchor.X - AutoScrollAnchor.Width / 2);
        Canvas.SetTop(AutoScrollAnchor, anchor.Y - AutoScrollAnchor.Height / 2);
        AutoScrollOverlay.Visibility = Visibility.Visible;
        Cursor = Cursors.ScrollAll;

        // CompositionTarget.Rendering выстреливает синхронно с каждым кадром
        // WPF — даёт более плавную картинку, чем DispatcherTimer, который
        // может стрелять в произвольный момент между кадрами.
        CompositionTarget.Rendering -= OnAutoScrollRendering;
        CompositionTarget.Rendering += OnAutoScrollRendering;
    }

    private void StopAutoScroll()
    {
        if (!_autoScrollActive)
        {
            return;
        }

        _autoScrollActive = false;
        _autoScrollCurrentSpeed = 0;
        CompositionTarget.Rendering -= OnAutoScrollRendering;
        AutoScrollOverlay.Visibility = Visibility.Collapsed;
        ClearValue(CursorProperty);
    }

    private void OnAutoScrollRendering(object? sender, EventArgs e)
    {
        if (!_autoScrollActive || _innerScrollViewer == null)
        {
            return;
        }

        var now = ((RenderingEventArgs)e).RenderingTime;
        if (_autoScrollLastFrameTime == TimeSpan.Zero)
        {
            // Первый кадр — нечего интегрировать, только запоминаем время.
            _autoScrollLastFrameTime = now;
            return;
        }

        var dt = (now - _autoScrollLastFrameTime).TotalSeconds;
        _autoScrollLastFrameTime = now;

        // Аномально большой dt (например, окно было свёрнуто) — пропускаем,
        // иначе рывок на сотни пикселей за один frame.
        if (dt <= 0 || dt > 0.1)
        {
            return;
        }

        var current = Mouse.GetPosition(AutoScrollOverlay);
        var delta = current.Y - _autoScrollAnchor.Y;
        var absDelta = Math.Abs(delta);

        double targetSpeed;
        if (absDelta < AutoScrollDeadZone)
        {
            targetSpeed = 0;
            Cursor = Cursors.ScrollAll;
        }
        else
        {
            var direction = Math.Sign(delta);
            var beyond = absDelta - AutoScrollDeadZone;
            // Quadratic curve: на маленьких смещениях скорость растёт мягко,
            // на больших — заметно быстрее. Гораздо приятнее линейного.
            targetSpeed = direction * beyond * beyond * AutoScrollSpeedFactor;
            Cursor = direction > 0 ? Cursors.ScrollS : Cursors.ScrollN;
        }

        // Экспоненциальный lerp текущей скорости к целевой. Frame-rate
        // independent: для любого dt 1 - exp(-rate × dt) даёт корректную
        // долю шага. Это убирает резкие старты/остановки.
        var lerp = 1 - Math.Exp(-AutoScrollSmoothingRate * dt);
        _autoScrollCurrentSpeed += (targetSpeed - _autoScrollCurrentSpeed) * lerp;

        var pxThisFrame = _autoScrollCurrentSpeed * dt;
        if (Math.Abs(pxThisFrame) > 0.001)
        {
            _innerScrollViewer.ScrollToVerticalOffset(
                _innerScrollViewer.VerticalOffset + pxThisFrame);
        }
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
