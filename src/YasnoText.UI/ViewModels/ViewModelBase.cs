using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YasnoText.UI.ViewModels;

/// <summary>
/// Базовый класс для всех ViewModel. Реализует INotifyPropertyChanged,
/// чтобы UI автоматически обновлялся при изменении свойств.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Сообщает UI, что свойство изменилось. Имя свойства определяется
    /// автоматически благодаря [CallerMemberName].
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Удобный метод: устанавливает поле, если значение изменилось,
    /// и автоматически отправляет уведомление в UI.
    /// </summary>
    /// <returns>true, если значение действительно изменилось.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
