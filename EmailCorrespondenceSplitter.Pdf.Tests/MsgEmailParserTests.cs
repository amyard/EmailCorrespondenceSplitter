using System.Text;
using EmailCorrespondenceSplitter.Pdf.Console.Models;
using EmailCorrespondenceSplitter.Pdf.Console.Services;

namespace EmailCorrespondenceSplitter.Pdf.Tests;

public class MsgEmailParserTests
{
    private readonly MsgEmailParser _parser;
    private readonly string _assetsDirectory;

    public MsgEmailParserTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _parser = new MsgEmailParser();
        _assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
    }

    [Fact]
    public void CanParse_WithMsgFile_ReturnsTrue()
    {
        var result = _parser.CanParse("test.msg");
        Assert.True(result);
    }

    [Fact]
    public void CanParse_WithNonMsgFile_ReturnsFalse()
    {
        Assert.False(_parser.CanParse("test.eml"));
        Assert.False(_parser.CanParse("test.txt"));
        Assert.False(_parser.CanParse("test.pdf"));
    }

    [Theory]
    [InlineData("em1.msg")]
    [InlineData("em2.msg")]
    [InlineData("em3.msg")]
    [InlineData("em4.msg")]
    [InlineData("em5.msg")]
    [InlineData("em6.msg")]
    public async Task ParseAsync_WithValidMsgFile_ReturnsEmailMessage(string fileName)
    {
        var filePath = Path.Combine(_assetsDirectory, fileName);
        
        if (!File.Exists(filePath))
        {
            return; // Skip if file doesn't exist
        }

        var result = await _parser.ParseAsync(filePath);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Subject));
        Assert.False(string.IsNullOrWhiteSpace(result.From));
        Assert.False(string.IsNullOrWhiteSpace(result.HtmlBody) && string.IsNullOrWhiteSpace(result.TextBody));
    }

    [Fact]
    public async Task ParseAsync_DetectsOutlookEmailType()
    {
        var filePath = Path.Combine(_assetsDirectory, "em1.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        var result = await _parser.ParseAsync(filePath);

        Assert.Equal(EmailType.Outlook, result.EmailType);
    }

    [Fact]
    public async Task ParseAsync_DetectsAppleEmailType()
    {
        var filePath = Path.Combine(_assetsDirectory, "em5.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        var result = await _parser.ParseAsync(filePath);

        Assert.Equal(EmailType.Apple, result.EmailType);
    }
}
