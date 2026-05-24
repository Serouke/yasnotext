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
        Action<ProfileItemViewModel>? onDelete = null,
        Action<ProfileItemViewModel>? onActivate = null,
        Action<ProfileItemViewModel>? onRename = null,
        Action<ProfileItemViewModel>? onOverwrite = null)
    {
        Profile = profile;
        Hotkey = hotkey;

        // Команды живут на самой карточке, а не на MainViewModel:
        // ContextMenu в WPF — отдельный visual tree (Popup), и
        // RelativeSource AncestorType=Window оттуда не находится.
        // DataContext меню = эта карточка, поэтому команды держим здесь.
        ActivateCommand = new RelayCommand(
            execute: _ => onActivate?.Invoke(this),
            canExecute: _ => onActivate != null);
        DeleteCommand = new RelayCommand(
            execute: _ => onDelete?.Invoke(this),
            canExecute: _ => onDelete != null && !Profile.IsBuiltIn);
        RenameCommand = new RelayCommand(
            execute: _ => onRename?.Invoke(this),
            canExecute: _ => onRename != null && !Profile.IsBuiltIn);
        OverwriteCommand = new RelayCommand(
            execute: _ => onOverwrite?.Invoke(this),
            canExecute: _ => onOverwrite != null && !Profile.IsBuiltIn);
    }

    /// <summary>Исходная модель профиля.</summary>
    public ReadingProfile Profile { get; }

    /// <summary>Название профиля для отображения.</summary>
    public string Name => Profile.Name;

    /// <summary>Текст с горячей клавишей: "Активен · Ctrl+1" или просто "Ctrl+1".</summary>
    public string HotkeyLabel => IsActive ? $"Активен · {Hotkey}" : Hotkey;

    /// <summary>Сама горячая клавиша без префикса.</summary>
    public string Hotkey { get; }

    /// <summary>Активация (выбор) профиля — для пункта меню «Активировать».</summary>
    public ICommand ActivateCommand { get; }

    /// <summary>Команда удаления профиля. Не выполняется на встроенных и
    /// если callback на удаление не передан в конструктор.</summary>
    public ICommand DeleteCommand { get; }

    /// <summary>Переименование пользовательского профиля.</summary>
    public ICommand RenameCommand { get; }

    /// <summary>Перезаписать профиль текущими настройками.</summary>
    public ICommand OverwriteCommand { get; }

    /// <summary>Принудительно дёргает PropertyChanged для Name — используется
    /// после переименования, потому что Name это просто прокси к Profile.Name.</summary>
    public void NotifyNameChanged()
    {
        OnPropertyChanged(nameof(Name));
    }

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
