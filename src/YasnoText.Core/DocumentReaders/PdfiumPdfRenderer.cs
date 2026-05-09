using System.Runtime.Versioning;
using PDFtoImage;
using SkiaSharp;

namespace YasnoText.Core.DocumentReaders;

/// <summary>
/// Реализация IPdfRenderer на базе PDFium через NuGet-обёртку PDFtoImage.
/// Нативные бинарники (pdfium) приезжают вместе с пакетом.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("android31.0")]
public class PdfiumPdfRenderer : IPdfRenderer
{
    public int GetPageCount(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            throw new ArgumentException("Путь к PDF не может быть пустым.", nameof(pdfPath));
        }

        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF не найден: {pdfPath}", pdfPath);
        }

        using var stream = File.OpenRead(pdfPath);
        return Conversion.GetPageCount(stream);
    }

    public byte[] RenderPageAsPng(string pdfPath, int pageIndex, int dpi = 300)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            throw new ArgumentException("Путь к PDF не может быть пустым.", nameof(pdfPath));
        }

        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF не найден: {pdfPath}", pdfPath);
        }

        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Индекс страницы должен быть >= 0.");
        }

        using var stream = File.OpenRead(pdfPath);
        using var bitmap = Conversion.ToImage(stream, page: new Index(pageIndex), options: new RenderOptions { Dpi = dpi });
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
