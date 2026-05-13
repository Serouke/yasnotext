using System.Runtime.Versioning;
using YasnoText.Core.DocumentReaders;

namespace YasnoText.Tests;

[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public class PdfiumPdfRendererTests
{
    private static string GetTestFilePath(string fileName)
    {
        var testFilesDir = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "test-files");
        return Path.GetFullPath(Path.Combine(testFilesDir, fileName));
    }

    [Fact]
    public void GetPageCount_SinglePagePdf_ReturnsOne()
    {
        var path = GetTestFilePath("simple-text.pdf");
        Assert.True(File.Exists(path));

        var pages = new PdfiumPdfRenderer().GetPageCount(path);

        Assert.Equal(1, pages);
    }

    [Fact]
    public void GetPageCount_MultipagePdf_ReturnsThree()
    {
        var path = GetTestFilePath("multipage-text.pdf");
        Assert.True(File.Exists(path));

        var pages = new PdfiumPdfRenderer().GetPageCount(path);

        Assert.Equal(3, pages);
    }

    [Fact]
    public void RenderPageAsPng_ReturnsValidPngBytes()
    {
        var path = GetTestFilePath("simple-text.pdf");
        Assert.True(File.Exists(path));

        var bytes = new PdfiumPdfRenderer().RenderPageAsPng(path, 0, dpi: 150);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100, "PNG слишком маленький, скорее всего пустой.");
        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    [Fact]
    public void GetPageCount_NonExistentFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(
            () => new PdfiumPdfRenderer().GetPageCount("/no/such.pdf"));
    }

    [Fact]
    public void RenderPageAsPng_NegativePageIndex_ThrowsArgumentOutOfRange()
    {
        var path = GetTestFilePath("simple-text.pdf");
        Assert.True(File.Exists(path));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PdfiumPdfRenderer().RenderPageAsPng(path, -1));
    }
}
