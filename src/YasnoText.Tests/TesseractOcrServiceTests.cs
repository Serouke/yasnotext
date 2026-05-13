using YasnoText.Core.Ocr;

namespace YasnoText.Tests;

public class TesseractOcrServiceTests
{
    [Fact]
    public void Ctor_NullOrEmptyTessdataPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new TesseractOcrService("", "eng"));
        Assert.Throws<ArgumentException>(() => new TesseractOcrService(null!, "eng"));
    }

    [Fact]
    public void Ctor_NullOrEmptyLanguages_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new TesseractOcrService("tessdata", ""));
        Assert.Throws<ArgumentException>(() => new TesseractOcrService("tessdata", null!));
    }

    [Fact]
    public void Recognize_WithMissingTessdata_ThrowsDirectoryNotFoundException()
    {
        // Tessdata не существует — ошибка проявляется только при первом вызове,
        // а не в конструкторе (lazy init не должен валить старт приложения).
        using var service = new TesseractOcrService("/no/such/tessdata", "eng");

        Assert.Throws<DirectoryNotFoundException>(
            () => service.Recognize(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void Recognize_NullOrEmptyImagePath_ThrowsArgumentException()
    {
        var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        using var service = new TesseractOcrService(tessdataPath, "eng");

        Assert.Throws<ArgumentException>(() => service.Recognize(""));
        Assert.Throws<ArgumentException>(() => service.Recognize((string)null!));
    }

    [Fact]
    public void Recognize_EmptyByteArray_ThrowsArgumentException()
    {
        var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        using var service = new TesseractOcrService(tessdataPath, "eng");

        Assert.Throws<ArgumentException>(() => service.Recognize(Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => service.Recognize((byte[])null!));
    }

    [Fact]
    public void Recognize_NonExistentImage_ThrowsFileNotFoundException()
    {
        var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        using var service = new TesseractOcrService(tessdataPath, "eng");

        Assert.Throws<FileNotFoundException>(
            () => service.Recognize("/non/existent/photo.png"));
    }
}
