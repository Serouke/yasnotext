using System.Windows.Input;

namespace YasnoText.UI.ViewModels;

/// <summary>
/// Простая реализация ICommand для MVVM. Используется, чтобы
/// привязывать действия (клики, горячие клавиши) к методам ViewModel.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>Перегрузка для команд без параметра.</summary>
    public RelayCommand(Action execute)
        : this(_ => execute(), null)
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}
