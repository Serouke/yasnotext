namespace YasnoText.Core.Profiles;

/// <summary>
/// Абстракция для применения темы оформления.
/// Core ничего не знает о WPF, поэтому объявляет интерфейс,
/// а реализация живёт в UI (см. WpfThemeApplier).
///
/// Это даёт два преимущества:
/// 1. Core можно тестировать без WPF-контекста.
/// 2. В будущем тему можно применять не только к WPF
///    (например, к веб-версии).
/// </summary>
public interface IThemeApplier
{
    /// <summary>
    /// Применить тему по её идентификатору.
    /// </summary>
    /// <param name="themeId">Идентификатор темы: "standard", "low-vision", "dyslexia".
    /// Берётся из ReadingProfile.BaseThemeId, чтобы пользовательские профили
    /// наследовали тему от того встроенного, на основе которого созданы.</param>
    void Apply(string themeId);
}
