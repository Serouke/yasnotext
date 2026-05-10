using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using YasnoText.Core.DocumentReaders;
using YasnoText.Core.Ocr;
using YasnoText.Core.Profiles;

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

    public MainViewModel(IThemeApplier themeApplier)
    {
        _themeApplier = themeApplier;
        _profileManager = new ProfileManager();
        _recentFilesService = new RecentFilesService();

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
            _profileManager.LoadAll().Select(p => new ProfileItemViewModel(
                p,
                hotkeys.TryGetValue(p.Id, out var hk) ? hk : string.Empty,
                onDelete: DeleteProfile)));

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
    public ICommand ExitCommand { get; }
    public ICommand ShowAboutCommand { get; }

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

        var newName = GenerateUniqueProfileName("Мой профиль");
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

        var newVm = new ProfileItemViewModel(
            newProfile,
            hotkey: string.Empty,
            onDelete: DeleteProfile);
        Profiles.Add(newVm);

        _profileManager.SaveUserProfiles(Profiles.Select(p => p.Profile));

        ActivateProfile(newVm);
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
        _profileManager.SaveUserProfiles(Profiles.Select(p => p.Profile));

        if (wasActive)
        {
            ActivateProfile(Profiles.First());
        }
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
        DocumentText = string.Empty;
        DocumentInfo = "Документ не открыт";
        HasDocument = false;
        _currentDocumentPath = null;
    }
}
