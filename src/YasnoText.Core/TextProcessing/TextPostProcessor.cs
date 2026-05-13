using System.Text.RegularExpressions;

namespace YasnoText.Core.TextProcessing;

/// <summary>
/// Постобработка распознанного текста: чинит типичные артефакты OCR.
/// Сейчас умеет только склеивать слова, разорванные переносом строки —
/// этого достаточно для большинства сканов.
///
/// Чего НЕ делает:
/// — не склеивает слова без пробелов (нужен словарь, см. CONTEXT.md грабля #6);
/// — не различает дефис-перенос и дефис в составных словах
///   («красно-белый» на конце строки превратится в «красноbелый» — компромисс).
/// </summary>
public static class TextPostProcessor
{
    // Буква + дефис + перевод строки + буква → склеить.
    // \p{L} ловит и кириллицу, и латиницу.
    private static readonly Regex SoftHyphenRegex =
        new(@"(\p{L})-\r?\n(\p{L})", RegexOptions.Compiled);

    public static string FixSoftHyphenLineBreaks(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return SoftHyphenRegex.Replace(text, "$1$2");
    }
}
