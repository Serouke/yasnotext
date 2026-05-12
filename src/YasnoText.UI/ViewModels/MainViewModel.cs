using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using YasnoText.Core.DocumentReaders;
using YasnoText.Core.Ocr;
using YasnoText.Core.Profiles;
using YasnoText.Core.Tts;

namespace YasnoText.UI.ViewModels;

/// <summary>
/// Главный ViewModel окна. Хранит коллекцию профилей,
/// текущий активный профиль, открытый документ, текущие настройки
/// чтения (которые можно править слайдерами) и команды.
/// </summary>
public class MainViewModel : ViewModelBase
{
    /// <summary>
    /// Фиксированный список шрифтов, которые точно есть в Windows и
    /// поддерживают кириллицу. Намеренно ограничен, чтобы пользователь
    /// не выбирал случайно несовместимый шрифт из 200+ системных.
    /// </summary>
    public static readonly IReadOnlyList<string> AvailableFontsList = new[]
    {
        "Segoe UI",
        "Arial",
        "Verdana",
        "Tahoma",
        "Calibri",
        "Georgia",
        "Times New Roman",
        "Comic Sans MS",
        "Consolas",
        "OpenDyslexic"
    };

    private const double MinFontSize = 8;
    private const double MaxFontSize = 72;
    private const double FontStep = 1;

    /// <summary>Сколько пользовательских профилей разрешено хранить.
    /// Со встроенными вместе получается до 10 в списке.</summary>
    private const int MaxUserProfiles = 7;

    private readonly IThemeApplier _themeApplier;
    private readonly ProfileManager _profileManager;
    private readonly RecentFilesService _recentFilesService;
    private readonly IDocumentReader[] _readers;
    private readonly ITextToSpeechService _ttsService;

    private ProfileItemViewModel? _activeProfile;
    private string _documentText = string.Empty;
    private string _documentInfo = "Документ не открыт";
    private bool _isLoading;
    private bool _hasDocument;
    private string? _currentDocumentPath;
    private bool _isReadingMode;

    private string _currentFontFamily = "Segoe UI";
    private double _currentFontSize = 14;
    private double _currentLineHeight = 1.5;

    public MainViewModel(IThemeApplier themeApplier, ITextToSpeechService ttsService)
    {
        _themeApplier = themeApplier;
        _profileManager = new ProfileManager();
        _recentFilesService = new RecentFilesService();
        _ttsService = ttsService;
        _ttsService.StateChanged += OnTtsStateChanged;

        var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        var ocr = new TesseractOcrService(tessdataPath, "eng+rus");
        var pdfRenderer = new PdfiumPdfRenderer();
        var pdfReader = new PdfTextOrOcrReader(
            textReader: new PdfTextReader(),
            ocrReader: new OcrPdfReader(pdfRenderer, ocr));

        _readers = new IDocumentReader[]
        {
            pdfReader,
            new DocxTextReader(),
            new OcrImageReader(ocr)
        };

        var hotkeys = new Dictionary<string, string>
        {
            { "standard", "Ctrl+1" },
            { "low-vision", "Ctrl+2" },
            { "dyslexia", "Ctrl+3" }
        };

        Profiles = new ObservableCollection<ProfileItemViewModel>(
            _profileManager.LoadAll().Select(p => CreateItemVm(
                p,
                hotkeys.TryGetValue(p.Id, out var hk) ? hk : string.Empty)));
        UserProfiles = new ObservableCollection<ProfileItemViewModel>(
            Profiles.Where(p => !p.Profile.IsBuiltIn));

        SelectProfileCommand = new RelayCommand(p =>
        {
            if (p is ProfileItemViewModel vm)
            {
                ActivateProfile(vm);
            }
        });

        OpenDocumentCommand = new RelayCommand(
            execute: async _ => await OpenViaDialogAsync(),
            canExecute: _ => !IsLoading);

        OpenRecentCommand = new RelayCommand(
            execute: async path =>
            {
                if (path is string filePath)
                {
                    await OpenFromPathAsync(filePath);
                }
            },
            canExecute: _ => !IsLoading);

        CloseDocumentCommand = new RelayCommand(
            execute: _ => CloseDocument(),
            canExecute: _ => HasDocument && !IsLoading);

        ToggleReadingModeCommand = new RelayCommand(_ => IsReadingMode = !IsReadingMode);

        PlayPauseCommand = new RelayCommand(
            execute: _ => TogglePlayPause(),
            canExecute: _ => HasDocument && !string.IsNullOrWhiteSpace(DocumentText));

        StopSpeechCommand = new RelayCommand(
            execute: _ => _ttsService.Stop(),
            canExecute: _ => _ttsService.State != SpeechState.Stopped);

        IncreaseFontCommand = new RelayCommand(
            execute: _ => CurrentFontSize = Math.Min(MaxFontSize, CurrentFontSize + FontStep),
            canExecute: _ => CurrentFontSize < MaxFontSize);

        DecreaseFontCommand = new RelayCommand(
            execute: _ => CurrentFontSize = Math.Max(MinFontSize, CurrentFontSize - FontStep),
            canExecute: _ => CurrentFontSize > MinFontSize);

        SaveProfileCommand = new RelayCommand(_ => SaveCurrentAsProfile());

        ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());

        ShowAboutCommand = new RelayCommand(_ => MessageBox.Show(
            "ЯсноТекст" + Environment.NewLine + Environment.NewLine +
            "Превращает «недоступные» PDF, DOCX и сканы в удобную среду для " +
            "чтения людьми с нарушениями зрения и дислексией." + Environment.NewLine + Environment.NewLine +
            "Учебный проект, MIT License.",
            "О программе",
            MessageBoxButton.OK,
            MessageBoxImage.Information));

        RecentFiles = new ObservableCollection<string>(_recentFilesService.Load());

        // По умолчанию активен Стандартный профиль.
        ActivateProfile(Profiles.First());
    }

    public ObservableCollection<ProfileItemViewModel> Profiles { get; }

    /// <summary>Только пользовательские профили — для подменю
    /// «Сохранить в существующий» и для drag-n-drop reorder.</summary>
    public ObservableCollection<ProfileItemViewModel> UserProfiles { get; }

    /// <summary>Есть ли пользовательские профили (для IsEnabled подменю).</summary>
    public bool HasUserProfiles => UserProfiles.Count > 0;

    public IReadOnlyList<string> AvailableFonts => AvailableFontsList;

    public ProfileItemViewModel? ActiveProfile
    {
        get => _activeProfile;
        private set
        {
            if (SetProperty(ref _activeProfile, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string DocumentText
    {
        get => _documentText;
        set => SetProperty(ref _documentText, value);
    }

    public string DocumentInfo
    {
        get => _documentInfo;
        private set => SetProperty(ref _documentInfo, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>true, если открыт реальный документ. До первой загрузки UI
    /// показывает onboarding-экран вместо области чтения.</summary>
    public bool HasDocument
    {
        get => _hasDocument;
        private set
        {
            if (SetProperty(ref _hasDocument, value))
            {
                OnPropertyChanged(nameof(HasNoDocument));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Удобный инверс для Visibility-биндинга без конвертера.</summary>
    public bool HasNoDocument => !_hasDocument;

    /// <summary>Режим «только чтение» — скрывает меню, toolbar, sidebar и status bar.</summary>
    public bool IsReadingMode
    {
        get => _isReadingMode;
        set
        {
            if (SetProperty(ref _isReadingMode, value))
            {
                OnPropertyChanged(nameof(IsNotReadingMode));
            }
        }
    }

    /// <summary>Инверс для Visibility-биндинга элементов, видимых вне reading mode.</summary>
    public bool IsNotReadingMode => !_isReadingMode;

    /// <summary>Шрифт, применяемый к области чтения в данный момент.</summary>
    public string CurrentFontFamily
    {
        get => _currentFontFamily;
        set => SetProperty(ref _currentFontFamily, value);
    }

    /// <summary>Размер шрифта (pt) — управляется слайдерами и кнопками A−/A+.</summary>
    public double CurrentFontSize
    {
        get => _currentFontSize;
        set
        {
            if (SetProperty(ref _currentFontSize, value))
            {
                OnPropertyChanged(nameof(EffectiveLineHeight));
                OnPropertyChanged(nameof(StatusText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Множитель межстрочного интервала (1.5 = полуторный).</summary>
    public double CurrentLineHeight
    {
        get => _currentLineHeight;
        set
        {
            if (SetProperty(ref _currentLineHeight, value))
            {
                OnPropertyChanged(nameof(EffectiveLineHeight));
            }
        }
    }

    /// <summary>
    /// Высота строки в device-independent pixels — то, что ожидает
    /// TextBlock.LineHeight. Множитель × текущий размер шрифта.
    /// </summary>
    public double EffectiveLineHeight => _currentFontSize * _currentLineHeight;

    public ICommand SelectProfileCommand { get; }
    public ICommand OpenDocumentCommand { get; }
    public ICommand OpenRecentCommand { get; }
    public ICommand CloseDocumentCommand { get; }
    public ICommand IncreaseFontCommand { get; }
    public ICommand DecreaseFontCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand ToggleReadingModeCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand StopSpeechCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand ShowAboutCommand { get; }

    /// <summary>true пока синтезатор реально говорит (для биндинга подсветки и иконки).</summary>
    public bool IsSpeaking => _ttsService.State == SpeechState.Speaking;

    /// <summary>true когда на паузе.</summary>
    public bool IsPaused => _ttsService.State == SpeechState.Paused;

    /// <summary>Динамическая надпись на кнопке «Озвучить»/«Пауза»/«Продолжить».</summary>
    public string PlayPauseLabel => _ttsService.State switch
    {
        SpeechState.Speaking => "Пауза",
        SpeechState.Paused => "Продолжить",
        _ => "Озвучить"
    };

    /// <summary>Для подписки в MainWindow.xaml.cs — handler подсветки текущего предложения.</summary>
    public event EventHandler<SpeechProgressEventArgs>? SpeechProgress
    {
        add => _ttsService.Progress += value;
        remove => _ttsService.Progress -= value;
    }

    /// <summary>Список путей к недавним файлам в порядке от свежего к старому.</summary>
    public ObservableCollection<string> RecentFiles { get; private set; } = new();

    /// <summary>true, если в списке недавних есть хоть один файл — для биндинга IsEnabled подменю.</summary>
    public bool HasRecentFiles => RecentFiles.Count > 0;

    public string StatusText
    {
        get
        {
            if (ActiveProfile == null)
            {
                return "Готов к работе";
            }

            return $"Профиль: {ActiveProfile.Profile.Name} · {CurrentFontSize:0}pt";
        }
    }

    public void ActivateById(string profileId)
    {
        var target = Profiles.FirstOrDefault(p => p.Profile.Id == profileId);
        if (target != null)
        {
            ActivateProfile(target);
        }
    }

    private void ActivateProfile(ProfileItemViewModel profileVm)
    {
        foreach (var p in Profiles)
        {
            p.IsActive = false;
        }

        profileVm.IsActive = true;
        ActiveProfile = profileVm;

        // Копируем настройки выбранного профиля в Current* — слайдеры/кнопки
        // отразят его значения и смогут править их без мутации самого профиля.
        var profile = profileVm.Profile;
        CurrentFontFamily = profile.FontFamily;
        CurrentFontSize = profile.FontSize;
        CurrentLineHeight = profile.LineHeight;

        _themeApplier.Apply(profile.BaseThemeId);
    }

    /// <summary>
    /// Создаёт новый пользовательский профиль из текущих настроек,
    /// сохраняет его на диск и активирует. Тема и цвета наследуются
    /// от того профиля, который был активен в момент сохранения.
    /// </summary>
    private void SaveCurrentAsProfile()
    {
        var userProfileCount = Profiles.Count(p => !p.Profile.IsBuiltIn);
        if (userProfileCount >= MaxUserProfiles)
        {
            MessageBox.Show(
                $"Нельзя хранить больше {MaxUserProfiles} пользовательских профилей. " +
                "Удалите ненужный через правый клик по карточке и попробуйте снова.",
                "ЯсноТекст",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var basis = ActiveProfile?.Profile ?? BuiltInProfiles.Standard;

        var defaultName = GenerateUniqueProfileName("Мой профиль");
        var dialog = new ProfileNameDialog(
            "Сохранить как новый профиль",
            $"Имя нового профиля (на основе «{basis.Name}»):",
            defaultName)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var newName = dialog.EnteredName;
        var newProfile = new ReadingProfile
        {
            Id = $"custom-{Guid.NewGuid():N}".Substring(0, 12),
            Name = newName,
            Description = $"На основе «{basis.Name}»",
            FontFamily = CurrentFontFamily,
            FontSize = CurrentFontSize,
            IsBold = basis.IsBold,
            // LetterSpacing наследуется от исходного — UI его не редактирует
            // (см. memory project_letter_spacing_dropped_from_ui).
            LetterSpacing = basis.LetterSpacing,
            LineHeight = CurrentLineHeight,
            WordSpacing = basis.WordSpacing,
            Colors = basis.Colors,
            IsBuiltIn = false,
            BaseThemeId = basis.BaseThemeId
        };

        var newVm = CreateItemVm(newProfile, hotkey: string.Empty);
        Profiles.Add(newVm);
        UserProfiles.Add(newVm);
        OnPropertyChanged(nameof(HasUserProfiles));

        _profileManager.SaveUserProfiles(Profiles.Select(p => p.Profile));

        ActivateProfile(newVm);
    }

    /// <summary>Фабричный метод для ProfileItemViewModel — подсовывает
    /// все нужные callback'и единообразно.</summary>
    private ProfileItemViewModel CreateItemVm(ReadingProfile profile, string hotkey)
    {
        return new ProfileItemViewModel(
            profile,
            hotkey,
            onDelete: DeleteProfile,
            onActivate: ActivateProfile,
            onRename: RenameProfile,
            onOverwrite: OverwriteProfile);
    }

    /// <summary>Спрашивает у пользователя новое имя и переименовывает профиль.</summary>
    private void RenameProfile(ProfileItemViewModel vm)
    {
        if (vm.Profile.IsBuiltIn)
        {
            return;
        }

        var dialog = new ProfileNameDialog(
            "Переименовать профиль",
            "Новое имя профиля:",
            vm.Profile.Name)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        vm.Profile.Name = dialog.EnteredName;
        vm.NotifyNameChanged();
        _profileManager.SaveUserProfiles(Profiles.Select(p => p.Profile));

        // Если переименовали активный — обновить статус-бар.
        if (ActiveProfile == vm)
        {
            OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>Перезаписывает выбранный профиль текущими настройками
    /// (FontFamily/Size/LineHeight). Имя, тема, цвета остаются.</summary>
    private void OverwriteProfile(ProfileItemViewModel vm)
    {
        if (vm.Profile.IsBuiltIn)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Перезаписать «{vm.Profile.Name}» текущими настройками шрифта и межстрочного интервала?",
            "ЯсноТекст",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        vm.Profile.FontFamily = CurrentFontFamily;
        vm.Profile.FontSize = CurrentFontSize;
        vm.Profile.LineHeight = CurrentLineHeight;
        _profileManager.SaveUserProfiles(Profiles.Select(p => p.Profile));

        // Если перезаписан активный — пере-применим, чтобы новые значения
        // попали в Current* (на случай, если пользователь крутил слайдеры
        // после активации этого профиля и перезаписывает «на лету»).
        if (ActiveProfile == vm)
        {
            ActivateProfile(vm);
        }
    }

    /// <summary>
    /// Удаляет пользовательский профиль (после подтверждения). Если он
    /// был активным — активируется первый профиль из списка
    /// (всегда «Стандартный», встроенные не удаляются).
    /// </summary>
    private void DeleteProfile(ProfileItemViewModel vm)
    {
        var confirm = MessageBox.Show(
            $"Удалить профиль «{vm.Profile.Name}»? Действие нельзя отменить.",
            "ЯсноТекст",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var wasActive = ActiveProfile == vm;
        Profiles.Remove(vm);
        UserProfiles.Remove(vm);
        OnPropertyChanged(nameof(HasUserProfiles));
        _profileManager.SaveUserProfiles(Profiles.Select(p => p.Profile));

        if (wasActive)
        {
            ActivateProfile(Profiles.First());
        }
    }

    /// <summary>
    /// Drag-n-drop reorder пользовательских профилей. Встроенные не двигаются
    /// и не могут стать целью drop'а — они всегда сверху списка.
    /// </summary>
    public void MoveProfile(ProfileItemViewModel source, ProfileItemViewModel target)
    {
        if (source == target) return;
        if (source.Profile.IsBuiltIn) return;
        if (target.Profile.IsBuiltIn) return;

        var sourceIdx = Profiles.IndexOf(source);
        var targetIdx = Profiles.IndexOf(target);
        if (sourceIdx < 0 || targetIdx < 0) return;

        Profiles.Move(sourceIdx, targetIdx);

        // UserProfiles держим синхронным — её используют меню/dnd.
        var userSrc = UserProfiles.IndexOf(source);
        var userDst = UserProfiles.IndexOf(target);
        if (userSrc >= 0 && userDst >= 0)
        {
            UserProfiles.Move(userSrc, userDst);
        }

        _profileManager.SaveUserProfiles(Profiles.Select(p => p.Profile));
    }

    private string GenerateUniqueProfileName(string baseName)
    {
        var existing = Profiles.Select(p => p.Profile.Name).ToHashSet();
        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        for (int i = 2; i < 1000; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{baseName} {Guid.NewGuid():N}".Substring(0, 30);
    }

    /// <summary>Показывает диалог выбора файла и открывает выбранный документ.</summary>
    private async Task OpenViaDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Открыть документ",
            Filter =
                "Документы и изображения (*.pdf;*.docx;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp)|" +
                    "*.pdf;*.docx;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp|" +
                "PDF документы (*.pdf)|*.pdf|" +
                "Word документы (*.docx)|*.docx|" +
                "Изображения (*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp)|" +
                    "*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp|" +
                "Все файлы (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await OpenFromPathAsync(dialog.FileName);
    }

    /// <summary>
    /// Открывает документ по готовому пути. Используется из диалога,
    /// списка недавних и drag-and-drop. Чтение файла выполняется в фоновом
    /// потоке, чтобы UI оставался отзывчивым даже на больших документах.
    /// </summary>
    public async Task OpenFromPathAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (IsLoading)
        {
            // Игнорируем повторный запрос пока идёт загрузка предыдущего —
            // например, если drop сработал во время Task.Run.
            return;
        }

        // Тот же документ уже открыт — повторное чтение бессмысленно.
        // Case-insensitive, потому что Windows-пути нормализуются по-разному
        // (drag из проводника vs recent vs ручной выбор).
        if (HasDocument &&
            _currentDocumentPath != null &&
            string.Equals(_currentDocumentPath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // При загрузке нового документа останавливаем озвучку — иначе синтезатор
        // продолжит читать прошлый текст, пока пользователь смотрит уже новый.
        _ttsService.Stop();

        var fileName = Path.GetFileName(filePath);

        if (!File.Exists(filePath))
        {
            // Файл из списка недавних мог быть удалён или перемещён.
            _recentFilesService.Remove(filePath);
            ReloadRecentFiles();

            MessageBox.Show(
                $"Файл «{fileName}» не найден. Возможно, он был удалён или перемещён.",
                "ЯсноТекст",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Файл нулевого размера ловим до парсера — иначе любой ридер
        // бросит малопонятное «invalid format» исключение.
        var fileLength = new FileInfo(filePath).Length;
        if (fileLength == 0)
        {
            MessageBox.Show(
                $"Файл «{fileName}» пустой (0 байт). Открывать нечего.",
                "ЯсноТекст",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var reader = _readers.FirstOrDefault(r => r.CanRead(filePath));
        if (reader == null)
        {
            MessageBox.Show(
                "Этот формат файла пока не поддерживается. " +
                "Поддерживаются PDF, DOCX и изображения.",
                "ЯсноТекст",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var isPdf = string.Equals(
            Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase);

        IsLoading = true;
        DocumentInfo = $"Открываю {fileName}...";

        try
        {
            var result = await Task.Run(() => reader.Read(filePath));

            // Документ есть, но в нём ноль страниц — корнер-кейс
            // (валидный заголовок без содержимого). Сообщение точнее,
            // чем «не найден текстовый слой».
            if (result.PageCount == 0)
            {
                MessageBox.Show(
                    $"В документе «{fileName}» нет ни одной страницы. " +
                    "Возможно, файл повреждён или сохранён без содержимого.",
                    "ЯсноТекст",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                DocumentText = "Документ не содержит страниц.";
                DocumentInfo = $"{fileName} · нет страниц";
                HasDocument = true;

                _recentFilesService.Add(filePath);
                ReloadRecentFiles();
                return;
            }

            if (result.IsEmpty)
            {
                var message = isPdf
                    ? "В этом PDF не найден текстовый слой. " +
                      "Возможно, это скан — функция OCR будет добавлена в следующей версии."
                    : "В этом документе не найден читаемый текст.";

                MessageBox.Show(
                    message,
                    "ЯсноТекст",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DocumentText = "В документе не найден текст.";
                DocumentInfo = isPdf
                    ? $"{fileName} · скан без OCR"
                    : $"{fileName} · пусто";
                HasDocument = true;
            }
            else
            {
                DocumentText = result.Text;
                DocumentInfo = $"{fileName} · {result.PageCount} стр.";
                HasDocument = true;
            }

            // Запоминаем файл в списке недавних только при успешном открытии.
            _recentFilesService.Add(filePath);
            ReloadRecentFiles();
            _currentDocumentPath = filePath;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось открыть документ: {ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            DocumentInfo = "Документ не открыт";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ReloadRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var path in _recentFilesService.Load())
        {
            RecentFiles.Add(path);
        }
        OnPropertyChanged(nameof(HasRecentFiles));
    }

    /// <summary>Закрывает текущий документ — возвращает onboarding-экран.</summary>
    private void CloseDocument()
    {
        _ttsService.Stop();
        DocumentText = string.Empty;
        DocumentInfo = "Документ не открыт";
        HasDocument = false;
        _currentDocumentPath = null;
    }

    private void TogglePlayPause()
    {
        switch (_ttsService.State)
        {
            case SpeechState.Stopped:
                _ttsService.Speak(DocumentText);
                break;
            case SpeechState.Speaking:
                _ttsService.Pause();
                break;
            case SpeechState.Paused:
                _ttsService.Resume();
                break;
        }
    }

    private void OnTtsStateChanged(object? sender, EventArgs e)
    {
        // События могут приходить из non-UI потока (синтезатор работает в своём).
        // Маршалим на UI-thread.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(NotifyTtsStateChanged);
        }
        else
        {
            NotifyTtsStateChanged();
        }
    }

    private void NotifyTtsStateChanged()
    {
        OnPropertyChanged(nameof(IsSpeaking));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(PlayPauseLabel));
        CommandManager.InvalidateRequerySuggested();
    }
}
