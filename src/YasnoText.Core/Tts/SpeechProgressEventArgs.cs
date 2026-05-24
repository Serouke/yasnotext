namespace YasnoText.Core.Tts;

/// <summary>
/// Прогресс озвучки: позиция текущего произносимого фрагмента в исходной
/// строке и его длина (в символах). Подсветке достаточно offset/length —
/// она сама найдёт нужный Run в FlowDocument по этим координатам.
/// </summary>
public class SpeechProgressEventArgs : EventArgs
{
    public SpeechProgressEventArgs(int characterPosition, int characterCount)
    {
        CharacterPosition = characterPosition;
        CharacterCount = characterCount;
    }

    public int CharacterPosition { get; }
    public int CharacterCount { get; }
}
