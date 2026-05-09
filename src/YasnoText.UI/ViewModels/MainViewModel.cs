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

    private readonly IThemeApplier _themeApplier;
    private readonly ProfileManager _profileManager;
    private readonly IDocumentReader[] _readers;

    private ProfileItemViewModel? _activeProfile;
    private string _documentText = string.Empty;
    private string _documentInfo = "Документ не открыт";
    private bool _isLoading;

    private string _currentFontFamily = "Segoe UI";
    private double _currentFontSize = 14;
    private double _currentLetterSpacing;
    private double _currentLineHeight = 1.5;

    public MainViewModel(IThemeApplier themeApplier)
    {
        _themeApplier = themeApplier;
        _profileManager = new ProfileManager();

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
                hotkeys.TryGetValue(p.Id, out var hk) ? hk : string.Empty)));

        SelectProfileCommand = new RelayCommand(p =>
        {
            if (p is ProfileItemViewModel vm)
            {
                ActivateProfile(vm);
            }
        });

        OpenDocumentCommand = new RelayCommand(
            execute: async _ => await OpenDocumentAsync(),
            canExecute: _ => !IsLoading);

        IncreaseFontCommand = new RelayCommand(
            execute: _ => CurrentFontSize = Math.Min(MaxFontSize, CurrentFontSize + FontStep),
            canExecute: _ => CurrentFontSize < MaxFontSize);

        DecreaseFontCommand = new RelayCommand(
            execute: _ => CurrentFontSize = Math.Max(MinFontSize, CurrentFontSize - FontStep),
            canExecute: _ => CurrentFontSize > MinFontSize);

        SaveProfileCommand = new RelayCommand(_ => SaveCurrentAsProfile());

        // По умолчанию активен Стандартный профиль.
        ActivateProfile(Profiles.First());

        // Приветственный текст до открытия документа.
        DocumentText =
            "Добро пожаловать в ЯсноТекст." + Environment.NewLine + Environment.NewLine +
            "Это приложение помогает превратить «недоступные» цифровые документы " +
            "в удобную среду для людей с нарушениями зрения и дислексией." + Environment.NewLine + Environment.NewLine +
            "Чтобы начать, нажмите кнопку «Открыть» на панели инструментов или " +
            "используйте сочетание клавиш Ctrl+O." + Environment.NewLine + Environment.NewLine +
            "Выберите подходящий профиль слева — интерфейс автоматически подстроится " +
            "под ваши потребности. Также можно использовать горячие клавиши: " +
            "Ctrl+1, Ctrl+2, Ctrl+3.";
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

    /// <summary>
    /// Дополнительный межбуквенный интервал в пикселях. Хранится в профиле,
    /// но к области чтения сейчас не применяется: WPF TextBlock/TextBox не
    /// поддерживает letter spacing нативно, переход на FlowDocument запланирован
    /// отдельно.
    /// </summary>
    public double CurrentLetterSpacing
    {
        get => _currentLetterSpacing;
        set => SetProperty(ref _currentLetterSpacing, value);
    }

    /// <summary>Множитель высоты строки (1.5 = полуторный).</summary>
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
    public ICommand IncreaseFontCommand { get; }
    public ICommand DecreaseFontCommand { get; }
    public ICommand SaveProfileCommand { get; }

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
        CurrentLetterSpacing = profile.LetterSpacing;
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
            LetterSpacing = CurrentLetterSpacing,
            LineHeight = CurrentLineHeight,
            WordSpacing = basis.WordSpacing,
            Colors = basis.Colors,
            IsBuiltIn = false,
            BaseThemeId = basis.BaseThemeId
        };

        var newVm = new ProfileItemViewModel(newProfile, hotkey: string.Empty);
        Profiles.Add(newVm);

        _profileManager.SaveUserProfiles(Profiles.Select(p => p.Profile));

        ActivateProfile(newVm);
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

    /// <summary>
    /// Открывает документ асинхронно. Чтение файла выполняется в фоновом
    /// потоке, чтобы UI оставался отзывчивым даже при работе с большими
    /// документами.
    /// </summary>
    private async Task OpenDocumentAsync()
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

        var filePath = dialog.FileName;
        var fileName = Path.GetFileName(filePath);

        var reader = _readers.FirstOrDefault(r => r.CanRead(filePath));
        if (reader == null)
        {
            MessageBox.Show(
                "Этот формат файла пока не поддерживается. " +
                "Поддерживаются PDF и DOCX.",
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
                return;
            }

            DocumentText = result.Text;
            DocumentInfo = $"{fileName} · {result.PageCount} стр.";
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
}
