using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using YasnoText.Core.DocumentReaders;
using YasnoText.Core.Profiles;

namespace YasnoText.UI.ViewModels;

/// <summary>
/// Главный ViewModel окна. Хранит коллекцию профилей,
/// текущий активный профиль, открытый документ и команды.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IThemeApplier _themeApplier;
    private readonly IDocumentReader _pdfReader;

    private ProfileItemViewModel? _activeProfile;
    private string _documentText = string.Empty;
    private string _documentInfo = "Документ не открыт";

    public MainViewModel(IThemeApplier themeApplier)
    {
        _themeApplier = themeApplier;
        _pdfReader = new PdfTextReader();

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

        OpenDocumentCommand = new RelayCommand(_ => OpenDocument());

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

    private void OpenDocument()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Открыть документ",
            Filter = "PDF документы (*.pdf)|*.pdf|Все файлы (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (!_pdfReader.CanRead(dialog.FileName))
            {
                MessageBox.Show(
                    "Этот формат файла пока не поддерживается. " +
                    "Откройте PDF-документ.",
                    "ЯсноТекст",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var result = _pdfReader.Read(dialog.FileName);

            if (result.IsEmpty)
            {
                MessageBox.Show(
                    "В этом PDF не найден текстовый слой. " +
                    "Возможно, это скан — функция OCR будет добавлена в следующей версии.",
                    "ЯсноТекст",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DocumentText = "В документе не найден текст.";
                DocumentInfo = $"{Path.GetFileName(dialog.FileName)} · скан без OCR";
                return;
            }

            DocumentText = result.Text;
            DocumentInfo = $"{Path.GetFileName(dialog.FileName)} · {result.PageCount} стр.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось открыть документ: {ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
