namespace YasnoText.Core.Tts;

/// <summary>
/// Сервис озвучивания текста (Text-to-Speech). Конкретная реализация
/// (System.Speech на Windows) живёт в UI-слое, Core знает только
/// интерфейс — чтобы было удобно тестировать и при желании заменить
/// движок.
/// </summary>
public interface ITextToSpeechService : IDisposable
{
    /// <summary>Текущее состояние синтезатора.</summary>
    SpeechState State { get; }

    /// <summary>Сообщает о произносимом фрагменте текста (слово/предложение).</summary>
    event EventHandler<SpeechProgressEventArgs>? Progress;

    /// <summary>Состояние сменилось — UI должен переоценить кнопки.</summary>
    event EventHandler? StateChanged;

    /// <summary>Начать озвучивание. Если уже что-то говорится — сбрасывается.</summary>
    void Speak(string text);

    /// <summary>Поставить на паузу (только если Speaking).</summary>
    void Pause();

    /// <summary>Снять паузу (только если Paused).</summary>
    void Resume();

    /// <summary>Остановить полностью. State становится Stopped.</summary>
    void Stop();
}
