using System.Speech.Synthesis;
using YasnoText.Core.Tts;

namespace YasnoText.UI.Tts;

/// <summary>
/// Реализация ITextToSpeechService поверх System.Speech (Windows).
/// На старте пытается выбрать русский голос (Pavel/Irina/любой ru-XX);
/// если такого в системе нет — используется голос по умолчанию.
/// </summary>
public sealed class SystemSpeechService : ITextToSpeechService
{
    private readonly SpeechSynthesizer _synth;
    private SpeechState _state = SpeechState.Stopped;
    private bool _disposed;

    public SystemSpeechService()
    {
        _synth = new SpeechSynthesizer();
        _synth.SetOutputToDefaultAudioDevice();

        _synth.SpeakProgress += OnSpeakProgress;
        _synth.StateChanged += OnSynthStateChanged;

        TrySelectRussianVoice();
    }

    public SpeechState State => _state;
    public event EventHandler<SpeechProgressEventArgs>? Progress;
    public event EventHandler? StateChanged;

    public void Speak(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Сбрасываем предыдущую очередь, иначе SpeakAsync встанет в хвост.
        _synth.SpeakAsyncCancelAll();
        _synth.SpeakAsync(text);
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_synth.State == SynthesizerState.Speaking)
        {
            _synth.Pause();
        }
    }

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_synth.State == SynthesizerState.Paused)
        {
            _synth.Resume();
        }
    }

    public void Stop()
    {
        if (_disposed) return;
        _synth.SpeakAsyncCancelAll();
    }

    private void TrySelectRussianVoice()
    {
        try
        {
            var ru = _synth.GetInstalledVoices()
                .Where(v => v.Enabled)
                .FirstOrDefault(v =>
                    v.VoiceInfo.Culture?.TwoLetterISOLanguageName == "ru");
            if (ru != null)
            {
                _synth.SelectVoice(ru.VoiceInfo.Name);
            }
        }
        catch
        {
            // Голоса не критичны — fallback на системный по умолчанию.
        }
    }

    private void OnSpeakProgress(object? sender, SpeakProgressEventArgs e)
    {
        Progress?.Invoke(
            this,
            new SpeechProgressEventArgs(e.CharacterPosition, e.CharacterCount));
    }

    private void OnSynthStateChanged(object? sender, StateChangedEventArgs e)
    {
        var newState = e.State switch
        {
            SynthesizerState.Speaking => SpeechState.Speaking,
            SynthesizerState.Paused => SpeechState.Paused,
            _ => SpeechState.Stopped,
        };

        if (newState != _state)
        {
            _state = newState;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _synth.SpeakProgress -= OnSpeakProgress;
        _synth.StateChanged -= OnSynthStateChanged;
        try
        {
            _synth.SpeakAsyncCancelAll();
        }
        catch { /* shutting down */ }
        _synth.Dispose();
    }
}
