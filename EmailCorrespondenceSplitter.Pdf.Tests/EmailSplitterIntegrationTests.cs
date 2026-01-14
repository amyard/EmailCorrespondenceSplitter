using System.Text;
using EmailCorrespondenceSplitter.Pdf.Console.Services;

namespace EmailCorrespondenceSplitter.Pdf.Tests;

public class EmailSplitterIntegrationTests : IDisposable
{
    private readonly string _assetsDirectory;
    private readonly string _testOutputDirectory;
    private readonly EmailSplitter _splitter;

    public EmailSplitterIntegrationTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
        _testOutputDirectory = Path.Combine(Path.GetTempPath(), $"EmailSplitterIntegrationTests_{Guid.NewGuid():N}");
        _splitter = new EmailSplitter(_testOutputDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testOutputDirectory))
        {
            try
            {
                Directory.Delete(_testOutputDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task ProcessEmailAsync_Em1_Creates2Correspondences()
    {
        var filePath = Path.Combine(_assetsDirectory, "em1.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        await _splitter.ProcessEmailAsync(filePath);

        var outputFolders = Directory.GetDirectories(_testOutputDirectory);
        Assert.Single(outputFolders);

        var msgFiles = Directory.GetFiles(outputFolders[0], "*.msg");
        // 1 parent copy + 2 correspondences = 3 files
        Assert.Equal(3, msgFiles.Length);
        Assert.Single(msgFiles.Where(f => f.Contains("00_parent_")));
        Assert.Equal(2, msgFiles.Count(f => f.Contains("_correspondence_")));
    }

    [Fact]
    public async Task ProcessEmailAsync_Em3_Creates8Correspondences()
    {
        var filePath = Path.Combine(_assetsDirectory, "em3.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        await _splitter.ProcessEmailAsync(filePath);

        var outputFolders = Directory.GetDirectories(_testOutputDirectory);
        Assert.Single(outputFolders);

        var msgFiles = Directory.GetFiles(outputFolders[0], "*.msg");
        // 1 parent copy + 8 correspondences = 9 files
        Assert.Equal(9, msgFiles.Length);
        Assert.Single(msgFiles.Where(f => f.Contains("00_parent_")));
        Assert.Equal(8, msgFiles.Count(f => f.Contains("_correspondence_")));
    }

    [Fact]
    public async Task ProcessDirectoryAsync_ProcessesAllMsgFiles()
    {
        if (!Directory.Exists(_assetsDirectory))
        {
            return;
        }

        var msgFileCount = Directory.GetFiles(_assetsDirectory, "*.msg").Length;
        if (msgFileCount == 0)
        {
            return;
        }

        await _splitter.ProcessDirectoryAsync(_assetsDirectory);

        var outputFolders = Directory.GetDirectories(_testOutputDirectory);
        Assert.Equal(msgFileCount, outputFolders.Length);
    }

    [Fact]
    public async Task ProcessEmailAsync_CreatesCorrectFolderStructure()
    {
        var filePath = Path.Combine(_assetsDirectory, "em1.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        await _splitter.ProcessEmailAsync(filePath);

        var outputFolders = Directory.GetDirectories(_testOutputDirectory);
        Assert.Single(outputFolders);
        Assert.Contains("em1", outputFolders[0]);
    }

    [Fact]
    public async Task ProcessEmailAsync_CorrespondenceFilesAreNumberedCorrectly()
    {
        var filePath = Path.Combine(_assetsDirectory, "em3.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        await _splitter.ProcessEmailAsync(filePath);

        var outputFolders = Directory.GetDirectories(_testOutputDirectory);
        var msgFiles = Directory.GetFiles(outputFolders[0], "*_correspondence_*.msg")
            .Select(Path.GetFileName)
            .OrderBy(f => f)
            .ToList();

        // Verify files are numbered 01 through 08
        Assert.Equal(8, msgFiles.Count);
        Assert.StartsWith("01_", msgFiles[0]);
        Assert.StartsWith("02_", msgFiles[1]);
        Assert.StartsWith("08_", msgFiles[7]);
    }
}
