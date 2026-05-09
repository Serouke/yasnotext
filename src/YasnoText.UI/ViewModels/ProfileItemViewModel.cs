using System.Windows.Input;
using YasnoText.Core.Profiles;

namespace YasnoText.UI.ViewModels;

/// <summary>
/// Обёртка над ReadingProfile для отображения в UI.
/// Хранит ссылку на профиль и флаг IsActive,
/// который позволяет подсвечивать активную карточку.
/// </summary>
public class ProfileItemViewModel : ViewModelBase
{
    private bool _isActive;

    public ProfileItemViewModel(
        ReadingProfile profile,
        string hotkey,
        Action<ProfileItemViewModel>? onDelete = null)
    {
        Profile = profile;
        Hotkey = hotkey;

        // DeleteCommand живёт здесь, а не в MainViewModel: ContextMenu в WPF —
        // отдельный visual tree (Popup), и RelativeSource AncestorType=Window
        // оттуда не находится. DataContext меню = эта карточка, поэтому команду
        // удобнее держать на ней.
        DeleteCommand = new RelayCommand(
            execute: _ => onDelete?.Invoke(this),
            canExecute: _ => onDelete != null && !Profile.IsBuiltIn);
    }

    /// <summary>Исходная модель профиля.</summary>
    public ReadingProfile Profile { get; }

    /// <summary>Название профиля для отображения.</summary>
    public string Name => Profile.Name;

    /// <summary>Текст с горячей клавишей: "Активен · Ctrl+1" или просто "Ctrl+1".</summary>
    public string HotkeyLabel => IsActive ? $"Активен · {Hotkey}" : Hotkey;

    /// <summary>Сама горячая клавиша без префикса.</summary>
    public string Hotkey { get; }

    /// <summary>Команда удаления профиля. Не выполняется на встроенных и
    /// если callback на удаление не передан в конструктор.</summary>
    public ICommand DeleteCommand { get; }

    /// <summary>Признак активного профиля. Влияет на подсветку карточки.</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                // HotkeyLabel зависит от IsActive — нужно тоже обновить.
                OnPropertyChanged(nameof(HotkeyLabel));
            }
        }
    }
}
