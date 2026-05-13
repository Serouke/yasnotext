using YasnoText.Core.Profiles;

namespace YasnoText.Tests;

public class RecentFilesServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public RecentFilesServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"YasnoTextRecent_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Load_WithNoFile_ReturnsEmpty()
    {
        var service = new RecentFilesService(_testDirectory);

        var list = service.Load();

        Assert.Empty(list);
    }

    [Fact]
    public void Add_NewFile_PutsItOnTop()
    {
        var service = new RecentFilesService(_testDirectory);

        service.Add("C:/docs/a.pdf");
        service.Add("C:/docs/b.docx");

        var list = service.Load();
        Assert.Equal(new[] { "C:/docs/b.docx", "C:/docs/a.pdf" }, list);
    }

    [Fact]
    public void Add_DuplicateFile_MovesItToTopWithoutDuplicating()
    {
        // LRU-поведение: повторное открытие файла перемещает его наверх,
        // а не плодит дубликаты.
        var service = new RecentFilesService(_testDirectory);
        service.Add("C:/docs/a.pdf");
        service.Add("C:/docs/b.docx");
        service.Add("C:/docs/a.pdf");

        var list = service.Load();

        Assert.Equal(new[] { "C:/docs/a.pdf", "C:/docs/b.docx" }, list);
    }

    [Fact]
    public void Add_DuplicateFile_IsCaseInsensitive()
    {
        // Windows-пути не различают регистр — два пути с разным регистром
        // должны считаться одним и тем же.
        var service = new RecentFilesService(_testDirectory);
        service.Add("C:/docs/a.pdf");
        service.Add("C:/DOCS/A.PDF");

        var list = service.Load();

        Assert.Single(list);
    }

    [Fact]
    public void Add_BeyondLimit_TruncatesOldest()
    {
        var service = new RecentFilesService(_testDirectory);

        for (int i = 1; i <= RecentFilesService.MaxItems + 5; i++)
        {
            service.Add($"C:/docs/file{i}.pdf");
        }

        var list = service.Load();

        Assert.Equal(RecentFilesService.MaxItems, list.Count);
        // Самый свежий — последний добавленный.
        Assert.Equal($"C:/docs/file{RecentFilesService.MaxItems + 5}.pdf", list[0]);
        // Самый старый, который ещё не вытеснен.
        Assert.Equal($"C:/docs/file6.pdf", list[^1]);
    }

    [Fact]
    public void Remove_ExistingFile_RemovesIt()
    {
        var service = new RecentFilesService(_testDirectory);
        service.Add("C:/docs/a.pdf");
        service.Add("C:/docs/b.pdf");

        service.Remove("C:/docs/a.pdf");

        var list = service.Load();
        Assert.Equal(new[] { "C:/docs/b.pdf" }, list);
    }

    [Fact]
    public void Add_NullOrEmpty_ThrowsArgumentException()
    {
        var service = new RecentFilesService(_testDirectory);

        Assert.Throws<ArgumentException>(() => service.Add(""));
        Assert.Throws<ArgumentException>(() => service.Add(null!));
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsEmptyWithoutThrowing()
    {
        // Если recent.json повреждён — мы не должны валить приложение,
        // просто молча считаем, что списка нет.
        var service = new RecentFilesService(_testDirectory);
        File.WriteAllText(Path.Combine(_testDirectory, "recent.json"), "{ this is not valid json");

        var list = service.Load();

        Assert.Empty(list);
    }
}
