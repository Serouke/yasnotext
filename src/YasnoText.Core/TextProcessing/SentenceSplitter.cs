namespace YasnoText.Core.TextProcessing;

/// <summary>
/// Грубое разбиение текста на предложения для подсветки при озвучке.
/// Разделители — точка, восклицательный, вопросительный знаки (и их группы:
/// «?..», «!!!»), за которыми идёт пробел/перенос/конец строки. Эвристика
/// не идеальна (например, «г. Москва» будет разрезано), но для подсветки
/// очередности при TTS этого хватает.
/// </summary>
public static class SentenceSplitter
{
    /// <summary>Предложение и его смещение в исходной строке.</summary>
    public readonly record struct Sentence(int Offset, int Length, string Text);

    public static IReadOnlyList<Sentence> Split(string text)
    {
        var result = new List<Sentence>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (!IsTerminator(text[i]))
            {
                continue;
            }

            // Сворачиваем подряд идущие терминаторы: «?..», «!!!», «...».
            while (i + 1 < text.Length && IsTerminator(text[i + 1]))
            {
                i++;
            }

            // Конец предложения только если дальше пробел/перенос/конец.
            var nextIsBoundary = i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1]);
            if (!nextIsBoundary)
            {
                continue;
            }

            var end = i + 1;
            AddSentence(result, text, start, end);
            start = end;
        }

        // Хвост без терминатора.
        if (start < text.Length)
        {
            AddSentence(result, text, start, text.Length);
        }

        return result;
    }

    private static void AddSentence(List<Sentence> result, string text, int start, int end)
    {
        // Пропускаем ведущие пробелы — они должны принадлежать предыдущему предложению
        // или быть «пустыми» (после серии \n).
        var trimStart = start;
        while (trimStart < end && char.IsWhiteSpace(text[trimStart]))
        {
            trimStart++;
        }

        var length = end - trimStart;
        if (length <= 0)
        {
            return;
        }

        result.Add(new Sentence(trimStart, length, text.Substring(trimStart, length)));
    }

    private static bool IsTerminator(char c) => c is '.' or '!' or '?';
}
