using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using YasnoText.Core.TextProcessing;
using YasnoText.Core.Tts;
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

    /// <summary>Верхний предел абсолютной скорости (px/s). Защита от зависаний
    /// на огромных документах с большим шрифтом, где каждый ScrollToVerticalOffset
    /// заставляет WPF пересчитывать тяжёлый layout.</summary>
    private const double AutoScrollMaxSpeed = 2500;

    /// <summary>Верхняя клипа на dt одного кадра. Если кадр пришёл с лагом
    /// (например, ScrollToVerticalOffset подвис), не интегрируем огромный
    /// прыжок за «прошедшее» время — иначе вылетим в самый низ за один тик.</summary>
    private const double AutoScrollMaxFrameDt = 0.05;

    private readonly MainViewModel _viewModel;

    private bool _autoScrollActive;
    private Point _autoScrollAnchor;
    private double _autoScrollCurrentSpeed;
    private TimeSpan _autoScrollLastFrameTime;
    private ScrollViewer? _innerScrollViewer;

    /// <summary>Внутренний идентификатор формата для drag-and-drop профилей.</summary>
    private const string ProfileCardDragFormat = "YasnoText.ProfileCard";

    private Point? _profileDragStartPoint;
    private ProfileItemViewModel? _profileDragSource;

    /// <summary>Таймер, который скрывает «F11 — выйти» через ~3 секунды
    /// после входа в режим чтения / последнего движения мыши.</summary>
    private DispatcherTimer? _readingHintFadeTimer;

    /// <summary>Range каждого предложения в текущем FlowDocument:
    /// start/end — offset'ы в DocumentText, run — соответствующий WPF-Run для подсветки.</summary>
    private readonly List<(int start, int end, Run run)> _sentenceRuns = new();
    private Run? _currentlyHighlightedRun;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(
            new WpfThemeApplier(),
            new YasnoText.UI.Tts.SystemSpeechService());
        DataContext = _viewModel;

        // FlowDocument строится в code-behind, потому что у FlowDocument
        // нет ItemsSource или биндинга к строке — нужен явный пересбор
        // блоков при изменении текста или межстрочного интервала.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.SpeechProgress += OnSpeechProgress;
        UpdateReadingDocument();

        // Автопрокрутка. Middle-click ловим через PreviewMouseDown viewer'а;
        // выход (любой другой клик / Esc) — глобально на окне.
        ReadingViewer.PreviewMouseDown += OnReadingViewerPreviewMouseDown;
        PreviewMouseDown += OnWindowPreviewMouseDownForAutoScroll;
        PreviewKeyDown += OnWindowPreviewKeyDownForAutoScroll;

        // Drag-and-drop файла. FlowDocumentScrollViewer помечает drag-события
        // своим текстовым drag&drop'ом как handled, поэтому атрибуты на Window
        // (DragOver=...) перестают срабатывать после открытия документа. Через
        // AddHandler с handledEventsToo: true Window получает событие в любом
        // случае, и drag-into-window работает над любой частью UI.
        AddHandler(DragEnterEvent,
            new DragEventHandler(OnWindowDragEnter), handledEventsToo: true);
        AddHandler(DragOverEvent,
            new DragEventHandler(OnWindowDragOver), handledEventsToo: true);
        AddHandler(DragLeaveEvent,
            new DragEventHandler(OnWindowDragLeave), handledEventsToo: true);
        AddHandler(DropEvent,
            new DragEventHandler(OnWindowDrop), handledEventsToo: true);

        // Hint в reading mode: на mouse-move возвращается, через 3 секунды
        // снова уплывает. Реагируем на PreviewMouseMove самого окна.
        PreviewMouseMove += OnWindowPreviewMouseMoveForReadingHint;
        _viewModel.PropertyChanged += OnViewModelPropertyChangedForReadingHint;

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

        // Закрыть текущий документ.
        InputBindings.Add(new KeyBinding(
            _viewModel.CloseDocumentCommand,
            Key.W, ModifierKeys.Control));

        // Режим чтения — скрыть всё кроме текста.
        InputBindings.Add(new KeyBinding(
            _viewModel.ToggleReadingModeCommand,
            Key.F11, ModifierKeys.None));

        // Озвучка: F5 — старт/пауза/продолжить, Shift+F5 — стоп.
        InputBindings.Add(new KeyBinding(
            _viewModel.PlayPauseCommand,
            Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(
            _viewModel.StopSpeechCommand,
            Key.F5, ModifierKeys.Shift));

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

    private void OnWindowDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            DropOverlay.Visibility = Visibility.Visible;
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        // Курсор меняется на «копировать» только если перетаскивается файл.
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDragLeave(object sender, DragEventArgs e)
    {
        // DragLeave событие срабатывает при переходе курсора между дочерними
        // элементами окна. Скрываем overlay только если курсор реально вышел
        // за пределы окна, иначе видим мерцание при движении мыши над меню.
        var pos = e.GetPosition(this);
        if (pos.X <= 0 || pos.Y <= 0 ||
            pos.X >= ActualWidth || pos.Y >= ActualHeight)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;

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

        // Если озвучка ушла в Stopped — снимаем подсветку, чтобы последнее
        // прочитанное предложение не оставалось залитым.
        if (e.PropertyName == nameof(MainViewModel.IsSpeaking) ||
            e.PropertyName == nameof(MainViewModel.PlayPauseLabel))
        {
            if (!_viewModel.IsSpeaking && !_viewModel.IsPaused)
            {
                ClearSpeechHighlight();
            }
        }
    }

    /// <summary>
    /// Пересобирает FlowDocument из текущего DocumentText. Каждый параграф
    /// дополнительно разбивается на предложения через SentenceSplitter —
    /// под каждое предложение свой Run. Список (offset, end, run) хранится в
    /// _sentenceRuns: handler SpeechProgress находит по нему текущий Run и
    /// подсвечивает фон.
    /// </summary>
    private void UpdateReadingDocument()
    {
        ClearSpeechHighlight();
        _sentenceRuns.Clear();

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

        // Разбор с учётом глобальных offset'ов: System.Speech даёт CharacterPosition
        // относительно строки, скормленной в Speak() — это весь DocumentText. Чтобы
        // найти Run, мы знаем точную координату каждого предложения в этой строке.
        var pos = 0;
        while (pos <= text.Length)
        {
            var nextSepIdx = FindNextParagraphSeparator(text, pos, out var separatorLength);
            var paraEnd = nextSepIdx < 0 ? text.Length : nextSepIdx;
            var paraText = text.Substring(pos, paraEnd - pos);

            if (!string.IsNullOrEmpty(paraText))
            {
                var paragraph = new Paragraph
                {
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                };

                var sentences = SentenceSplitter.Split(paraText);
                if (sentences.Count == 0)
                {
                    // На случай если SentenceSplitter ничего не вернул (только пробелы):
                    // добавляем один Run на весь параграф, чтобы порядок Inlines был
                    // консистентен с текстом.
                    var run = new Run(paraText);
                    paragraph.Inlines.Add(run);
                    _sentenceRuns.Add((pos, pos + paraText.Length, run));
                }
                else
                {
                    var written = 0;
                    foreach (var s in sentences)
                    {
                        // Восстанавливаем «соединительные» пробелы между предложениями —
                        // они в исходном тексте есть, но SentenceSplitter их съел.
                        if (s.Offset > written)
                        {
                            paragraph.Inlines.Add(new Run(paraText.Substring(written, s.Offset - written)));
                        }

                        var run = new Run(s.Text);
                        paragraph.Inlines.Add(run);
                        _sentenceRuns.Add((pos + s.Offset, pos + s.Offset + s.Length, run));
                        written = s.Offset + s.Length;
                    }

                    if (written < paraText.Length)
                    {
                        paragraph.Inlines.Add(new Run(paraText.Substring(written)));
                    }
                }

                doc.Blocks.Add(paragraph);
            }

            if (nextSepIdx < 0) break;
            pos = nextSepIdx + separatorLength;
        }

        ReadingViewer.Document = doc;
    }

    /// <summary>Находит ближайший разделитель параграфов \r\n\r\n или \n\n,
    /// возвращает его offset и точную длину (4 или 2).</summary>
    private static int FindNextParagraphSeparator(string text, int startIndex, out int separatorLength)
    {
        var crlf = text.IndexOf("\r\n\r\n", startIndex, StringComparison.Ordinal);
        var lf = text.IndexOf("\n\n", startIndex, StringComparison.Ordinal);

        if (crlf < 0 && lf < 0)
        {
            separatorLength = 0;
            return -1;
        }
        if (crlf < 0)
        {
            separatorLength = 2;
            return lf;
        }
        if (lf < 0 || crlf <= lf)
        {
            separatorLength = 4;
            return crlf;
        }
        separatorLength = 2;
        return lf;
    }

    private void OnSpeechProgress(object? sender, SpeechProgressEventArgs e)
    {
        // SystemSpeechService поднимает событие из своего потока. На UI
        // нельзя трогать Run.Background, поэтому маршалим — BeginInvoke,
        // чтобы не блокировать TTS-callback.
        Dispatcher.BeginInvoke(
            new Action(() => HighlightSentenceAt(e.CharacterPosition)),
            DispatcherPriority.Background);
    }

    private void HighlightSentenceAt(int characterPosition)
    {
        // Ищем предложение, в которое попадает текущая позиция произнесения.
        // Список упорядочен по offset, поэтому достаточно линейного поиска —
        // даже на 1000 предложений это микросекунды.
        Run? target = null;
        foreach (var (start, end, run) in _sentenceRuns)
        {
            if (characterPosition >= start && characterPosition < end)
            {
                target = run;
                break;
            }
        }

        if (target == null || target == _currentlyHighlightedRun)
        {
            return;
        }

        ClearSpeechHighlight();

        target.Background = GetSentenceHighlightBrush();
        _currentlyHighlightedRun = target;

        // Подталкиваем viewport, чтобы предложение всегда было видно — иначе
        // на длинном документе озвучка убегает вниз, а текст стоит вверху.
        try { target.BringIntoView(); } catch { /* во время анимации может бросать */ }
    }

    private void ClearSpeechHighlight()
    {
        if (_currentlyHighlightedRun != null)
        {
            _currentlyHighlightedRun.Background = null;
            _currentlyHighlightedRun = null;
        }
    }

    /// <summary>Полупрозрачный AccentBrush текущей темы — на любой палитре виден.</summary>
    private Brush GetSentenceHighlightBrush()
    {
        if (Application.Current?.Resources["AccentBrush"] is SolidColorBrush accent)
        {
            var c = accent.Color;
            return new SolidColorBrush(Color.FromArgb(96, c.R, c.G, c.B));
        }
        return new SolidColorBrush(Color.FromArgb(96, 100, 180, 255));
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
        if (e.Key != Key.Escape)
        {
            return;
        }

        // Esc приоритетно гасит autoscroll. Если автопрокрутка не активна,
        // но пользователь в reading mode — Esc выходит из режима. Это
        // привычно по аналогии с fullscreen в браузерах/плеерах.
        if (_autoScrollActive)
        {
            StopAutoScroll();
            e.Handled = true;
        }
        else if (_viewModel.IsReadingMode)
        {
            _viewModel.IsReadingMode = false;
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

        if (dt <= 0)
        {
            return;
        }

        // Один лагнувший кадр (например, ScrollToVerticalOffset подвис на
        // тяжёлом FlowDocument) не должен превращаться в прыжок на сотни
        // пикселей. Клипуем dt сверху.
        if (dt > AutoScrollMaxFrameDt)
        {
            dt = AutoScrollMaxFrameDt;
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
            // Верхний предел — защита от зависаний на гигантских документах
            // с крупным шрифтом, где WPF тратит много времени на layout
            // при каждом ScrollToVerticalOffset.
            targetSpeed = Math.Clamp(targetSpeed, -AutoScrollMaxSpeed, AutoScrollMaxSpeed);
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

    private void OnProfileCardPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Сохраняем точку начала жеста — drag начинается только если
        // пользователь сдвинул мышь дальше системного порога. Голый клик
        // отрабатывает обычной InputBinding'ой (Activate).
        if (sender is FrameworkElement el && el.DataContext is ProfileItemViewModel vm
            && !vm.Profile.IsBuiltIn)
        {
            _profileDragStartPoint = e.GetPosition(null);
            _profileDragSource = vm;
        }
        else
        {
            _profileDragStartPoint = null;
            _profileDragSource = null;
        }
    }

    private void OnProfileCardMouseMove(object sender, MouseEventArgs e)
    {
        if (_profileDragStartPoint == null || _profileDragSource == null)
        {
            return;
        }
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _profileDragStartPoint = null;
            _profileDragSource = null;
            return;
        }

        var diff = e.GetPosition(null) - _profileDragStartPoint.Value;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is FrameworkElement el)
        {
            var data = new DataObject(ProfileCardDragFormat, _profileDragSource);
            DragDrop.DoDragDrop(el, data, DragDropEffects.Move);
        }

        _profileDragStartPoint = null;
        _profileDragSource = null;
    }

    private void OnProfileCardDrop(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            // Сбрасываем визуальный feedback от DragEnter.
            border.ClearValue(Border.BorderBrushProperty);
            border.ClearValue(Border.BorderThicknessProperty);
        }

        if (!e.Data.GetDataPresent(ProfileCardDragFormat))
        {
            return;
        }

        var source = e.Data.GetData(ProfileCardDragFormat) as ProfileItemViewModel;
        if (source == null) return;

        if (sender is FrameworkElement el &&
            el.DataContext is ProfileItemViewModel target)
        {
            _viewModel.MoveProfile(source, target);
        }

        // Останавливаем bubbling, чтобы дроп карточки не попал в Window-handler
        // (там обрабатывается дроп файлов из проводника).
        e.Handled = true;
    }

    private void OnProfileCardDragEnter(object sender, DragEventArgs e)
    {
        // Подсвечиваем только валидные цели: drag нашего формата, и target —
        // пользовательский профиль (на built-in бросить нельзя).
        if (!e.Data.GetDataPresent(ProfileCardDragFormat)) return;
        if (sender is not Border border) return;
        if (border.DataContext is not ProfileItemViewModel vm) return;
        if (vm.Profile.IsBuiltIn) return;

        border.BorderBrush = (Brush)FindResource("AccentBrush");
        border.BorderThickness = new Thickness(2.5);
    }

    private void OnProfileCardDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.ClearValue(Border.BorderBrushProperty);
            border.ClearValue(Border.BorderThicknessProperty);
        }
    }

    private void OnViewModelPropertyChangedForReadingHint(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsReadingMode)) return;

        if (_viewModel.IsReadingMode)
        {
            ShowReadingHint();
            ScheduleReadingHintFade();
        }
        else
        {
            _readingHintFadeTimer?.Stop();
            // Visibility управляется биндингом на IsReadingMode — здесь только
            // сбрасываем Opacity, чтобы при следующем входе hint снова был виден.
            ReadingModeHint.Opacity = 1;
        }
    }

    private void OnWindowPreviewMouseMoveForReadingHint(object sender, MouseEventArgs e)
    {
        if (!_viewModel.IsReadingMode) return;
        // Любое движение мыши в reading mode возвращает hint и пере-стартует таймер.
        ShowReadingHint();
        ScheduleReadingHintFade();
    }

    private void ShowReadingHint()
    {
        ReadingModeHint.BeginAnimation(UIElement.OpacityProperty, null);
        ReadingModeHint.Opacity = 1;
    }

    private void ScheduleReadingHintFade()
    {
        if (_readingHintFadeTimer == null)
        {
            _readingHintFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _readingHintFadeTimer.Tick += (_, _) =>
            {
                _readingHintFadeTimer!.Stop();
                if (!_viewModel.IsReadingMode) return;

                var fade = new DoubleAnimation
                {
                    From = ReadingModeHint.Opacity,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(600),
                };
                ReadingModeHint.BeginAnimation(UIElement.OpacityProperty, fade);
            };
        }

        _readingHintFadeTimer.Stop();
        _readingHintFadeTimer.Start();
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
