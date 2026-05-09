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
/// текущий активный профиль, открытый документ и команды.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IThemeApplier _themeApplier;
    private readonly IDocumentReader[] _readers;

    private ProfileItemViewModel? _activeProfile;
    private string _documentText = string.Empty;
    private string _documentInfo = "Документ не открыт";
    private bool _isLoading;

    public MainViewModel(IThemeApplier themeApplier)
    {
        _themeApplier = themeApplier;

        // OCR-сервис ленив: tessdata проверяется только при первом распознавании,
        // чтобы отсутствующие языковые модели не валили старт приложения.
        var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        var ocr = new TesseractOcrService(tessdataPath, "eng+rus");
        var pdfRenderer = new PdfiumPdfRenderer();

        // Композитный PDF-ридер: текстовый слой → fallback на OCR для сканов.
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
            BuiltInProfiles.All.Select(p => new ProfileItemViewModel(
                p,
                hotkeys.TryGetValue(p.Id, out var hk) ? hk : string.Empty)));

        SelectProfileCommand = new RelayCommand(p =>
        {
            if (p is ProfileItemViewModel vm)
            {
                ActivateProfile(vm);
            }
        });

        // Команда открытия документа: запрещена, пока идёт загрузка предыдущего.
        OpenDocumentCommand = new RelayCommand(
            execute: async _ => await OpenDocumentAsync(),
            canExecute: _ => !IsLoading);

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

    /// <summary>Текст текущего открытого документа (или приветствие).</summary>
    public string DocumentText
    {
        get => _documentText;
        set => SetProperty(ref _documentText, value);
    }

    /// <summary>Строка с информацией о документе для статусбара.</summary>
    public string DocumentInfo
    {
        get => _documentInfo;
        private set => SetProperty(ref _documentInfo, value);
    }

    /// <summary>true, пока документ читается в фоновом потоке.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                // Команды должны переоценить CanExecute, чтобы кнопка стала
                // disabled во время загрузки.
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ICommand SelectProfileCommand { get; }
    public ICommand OpenDocumentCommand { get; }

    public string StatusText
    {
        get
        {
            if (ActiveProfile == null)
            {
                return "Готов к работе";
            }

            var profile = ActiveProfile.Profile;
            return $"Профиль: {profile.Name} · {profile.FontSize}pt";
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
        _themeApplier.Apply(profileVm.Profile.Id);
    }

    /// <summary>
    /// Открывает документ асинхронно.
    /// Чтение файла выполняется в фоновом потоке, чтобы UI оставался отзывчивым
    /// даже при работе с большими документами.
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

        // Показываем индикатор загрузки. UI остаётся живым, потому что
        // тяжёлая работа уезжает в Task.Run.
        IsLoading = true;
        DocumentInfo = $"Открываю {fileName}...";

        try
        {
            // Чтение документа — операция, ограниченная процессором и диском.
            // Task.Run переносит её в пул потоков, освобождая UI-поток.
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

            // После await мы снова на UI-потоке, можно безопасно менять свойства.
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
