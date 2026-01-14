using System.Text;
using EmailCorrespondenceSplitter.Pdf.Console.Models;
using EmailCorrespondenceSplitter.Pdf.Console.Services;

namespace EmailCorrespondenceSplitter.Pdf.Tests;

public class OutputManagerTests : IDisposable
{
    private readonly OutputManager _outputManager;
    private readonly string _testOutputDirectory;

    public OutputManagerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _testOutputDirectory = Path.Combine(Path.GetTempPath(), $"EmailSplitterTests_{Guid.NewGuid():N}");
        _outputManager = new OutputManager(_testOutputDirectory);
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
    public void CreateEmailFolder_CreatesNewFolder()
    {
        var emailPath = Path.Combine(Path.GetTempPath(), "test_email.msg");

        var result = _outputManager.CreateEmailFolder(emailPath);

        Assert.True(Directory.Exists(result));
        Assert.Contains("test_email", result);
    }

    [Fact]
    public void CreateEmailFolder_AppendsNumber_WhenFolderExists()
    {
        var emailPath = Path.Combine(Path.GetTempPath(), "duplicate.msg");

        var first = _outputManager.CreateEmailFolder(emailPath);
        var second = _outputManager.CreateEmailFolder(emailPath);

        Assert.NotEqual(first, second);
        Assert.Contains("_1", second);
    }

    [Fact]
    public void CopyParentEmail_CopiesFileWithPrefix()
    {
        var tempSourceFile = Path.Combine(Path.GetTempPath(), $"source_{Guid.NewGuid():N}.msg");
        File.WriteAllText(tempSourceFile, "test content");

        try
        {
            var outputFolder = _outputManager.CreateEmailFolder(tempSourceFile);

            _outputManager.CopyParentEmail(tempSourceFile, outputFolder);

            var expectedFileName = $"00_parent_{Path.GetFileName(tempSourceFile)}";
            var expectedPath = Path.Combine(outputFolder, expectedFileName);
            
            Assert.True(File.Exists(expectedPath));
        }
        finally
        {
            if (File.Exists(tempSourceFile))
            {
                File.Delete(tempSourceFile);
            }
        }
    }

    [Fact]
    public async Task SaveCorrespondenceAsync_CreatesMsgFile()
    {
        var correspondence = new Correspondence
        {
            From = "sender@test.com",
            To = "recipient@test.com",
            Subject = "Test Subject",
            HtmlContent = "<p>Test content</p>",
            TextContent = "Test content",
            Index = 0,
            IsParent = true
        };

        var outputFolder = _outputManager.CreateEmailFolder(Path.Combine(Path.GetTempPath(), "test.msg"));

        await _outputManager.SaveCorrespondenceAsync(correspondence, outputFolder);

        var files = Directory.GetFiles(outputFolder, "*.msg");
        Assert.Single(files);
        // @ is sanitized to _ in filename
        Assert.Contains("01_correspondence_sender", files[0]);
    }

    [Fact]
    public async Task SaveCorrespondenceAsync_MultipleCorrespondences_CreatesMultipleFiles()
    {
        var correspondences = new[]
        {
            new Correspondence { From = "sender1@test.com", To = "recipient@test.com", Subject = "Test", Index = 0, IsParent = true, HtmlContent = "<p>Content 1</p>" },
            new Correspondence { From = "sender2@test.com", To = "recipient@test.com", Subject = "Test", Index = 1, IsParent = false, HtmlContent = "<p>Content 2</p>" },
            new Correspondence { From = "sender3@test.com", To = "recipient@test.com", Subject = "Test", Index = 2, IsParent = false, HtmlContent = "<p>Content 3</p>" }
        };

        var outputFolder = _outputManager.CreateEmailFolder(Path.Combine(Path.GetTempPath(), "multi.msg"));

        foreach (var correspondence in correspondences)
        {
            await _outputManager.SaveCorrespondenceAsync(correspondence, outputFolder);
        }

        var files = Directory.GetFiles(outputFolder, "*.msg");
        Assert.Equal(3, files.Length);
    }

    [Fact]
    public async Task SaveCorrespondenceAsync_WithSentDate_PreservesDate()
    {
        var sentDate = new DateTime(2024, 1, 15, 10, 30, 0);
        var correspondence = new Correspondence
        {
            From = "sender@test.com",
            To = "recipient@test.com",
            Subject = "Test",
            SentOn = sentDate,
            HtmlContent = "<p>Content</p>",
            Index = 0,
            IsParent = true
        };

        var outputFolder = _outputManager.CreateEmailFolder(Path.Combine(Path.GetTempPath(), "dated.msg"));

        await _outputManager.SaveCorrespondenceAsync(correspondence, outputFolder);

        var files = Directory.GetFiles(outputFolder, "*.msg");
        Assert.Single(files);
    }
}
