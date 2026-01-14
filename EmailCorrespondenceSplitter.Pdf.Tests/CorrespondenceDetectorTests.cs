using System.Text;
using EmailCorrespondenceSplitter.Pdf.Console.Models;
using EmailCorrespondenceSplitter.Pdf.Console.Services;

namespace EmailCorrespondenceSplitter.Pdf.Tests;

public class CorrespondenceDetectorTests
{
    private readonly CorrespondenceDetector _detector;
    private readonly MsgEmailParser _parser;
    private readonly string _assetsDirectory;

    public CorrespondenceDetectorTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _detector = new CorrespondenceDetector();
        _parser = new MsgEmailParser();
        _assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
    }

    [Fact]
    public void DetectCorrespondences_WithEmptyHtmlBody_ReturnsSingleCorrespondence()
    {
        var email = new EmailMessage
        {
            From = "sender@test.com",
            To = "recipient@test.com",
            Subject = "Test",
            HtmlBody = "",
            TextBody = "Plain text content"
        };

        var result = _detector.DetectCorrespondences(email);

        Assert.Single(result);
        Assert.True(result[0].IsParent);
    }

    [Fact]
    public void DetectCorrespondences_WithNoQuotes_ReturnsSingleCorrespondence()
    {
        var email = new EmailMessage
        {
            From = "sender@test.com",
            To = "recipient@test.com",
            Subject = "Test",
            HtmlBody = "<html><body><p>Hello World</p></body></html>",
            EmailType = EmailType.Outlook
        };

        var result = _detector.DetectCorrespondences(email);

        Assert.Single(result);
        Assert.True(result[0].IsParent);
        Assert.Equal("sender@test.com", result[0].From);
    }

    [Fact]
    public async Task DetectCorrespondences_Em1_Returns2Correspondences()
    {
        var filePath = Path.Combine(_assetsDirectory, "em1.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        var email = await _parser.ParseAsync(filePath);
        var result = _detector.DetectCorrespondences(email);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].IsParent);
        Assert.False(result[1].IsParent);
    }

    [Fact]
    public async Task DetectCorrespondences_Em2_Returns2Correspondences()
    {
        var filePath = Path.Combine(_assetsDirectory, "em2.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        var email = await _parser.ParseAsync(filePath);
        var result = _detector.DetectCorrespondences(email);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task DetectCorrespondences_Em3_Returns8Correspondences()
    {
        var filePath = Path.Combine(_assetsDirectory, "em3.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        var email = await _parser.ParseAsync(filePath);
        var result = _detector.DetectCorrespondences(email);

        Assert.Equal(8, result.Count);
        Assert.True(result[0].IsParent);
        
        // Verify all others are not parent
        for (int i = 1; i < result.Count; i++)
        {
            Assert.False(result[i].IsParent);
        }
    }

    [Fact]
    public async Task DetectCorrespondences_Em4_Returns2Correspondences()
    {
        var filePath = Path.Combine(_assetsDirectory, "em4.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        var email = await _parser.ParseAsync(filePath);
        var result = _detector.DetectCorrespondences(email);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task DetectCorrespondences_Em5_AppleMail_Returns2Correspondences()
    {
        var filePath = Path.Combine(_assetsDirectory, "em5.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        var email = await _parser.ParseAsync(filePath);
        
        Assert.Equal(EmailType.Apple, email.EmailType);
        
        var result = _detector.DetectCorrespondences(email);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task DetectCorrespondences_Em6_Returns3Correspondences()
    {
        var filePath = Path.Combine(_assetsDirectory, "em6.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        var email = await _parser.ParseAsync(filePath);
        var result = _detector.DetectCorrespondences(email);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task DetectCorrespondences_ParentHasAttachments()
    {
        var filePath = Path.Combine(_assetsDirectory, "em1.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        var email = await _parser.ParseAsync(filePath);
        email.AttachmentData["test.pdf"] = new byte[] { 1, 2, 3 };
        
        var result = _detector.DetectCorrespondences(email);

        Assert.True(result[0].IsParent);
        Assert.Contains("test.pdf", result[0].Attachments.Keys);
        
        // Non-parent correspondences should not have attachments
        for (int i = 1; i < result.Count; i++)
        {
            Assert.DoesNotContain("test.pdf", result[i].Attachments.Keys);
        }
    }

    [Fact]
    public async Task DetectCorrespondences_CorrespondencesHaveCorrectIndexes()
    {
        var filePath = Path.Combine(_assetsDirectory, "em3.msg");
        
        if (!File.Exists(filePath))
        {
            return;
        }

        var email = await _parser.ParseAsync(filePath);
        var result = _detector.DetectCorrespondences(email);

        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(i, result[i].Index);
        }
    }

    [Fact]
    public void DetectCorrespondences_GmailFormat_SplitsCorrectly()
    {
        var email = new EmailMessage
        {
            From = "sender@gmail.com",
            To = "recipient@test.com",
            Subject = "Re: Test",
            HtmlBody = @"
                <div>My reply content</div>
                <div class=""gmail_quote"">
                    <div>On Jan 1, 2024, someone wrote:</div>
                    <div>Original message content</div>
                </div>",
            EmailType = EmailType.Gmail
        };

        var result = _detector.DetectCorrespondences(email);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].IsParent);
        Assert.Contains("My reply content", result[0].HtmlContent);
    }

    [Fact]
    public void DetectCorrespondences_OutlookHrFormat_SplitsCorrectly()
    {
        var email = new EmailMessage
        {
            From = "sender@outlook.com",
            To = "recipient@test.com",
            Subject = "RE: Test",
            HtmlBody = @"
                <div class=""MsoNormal"">
                    <p>My reply content</p>
                </div>
                <div><hr></div>
                <div>
                    <p><b>From:</b> Original Sender</p>
                    <p>Original message content</p>
                </div>",
            EmailType = EmailType.Outlook
        };

        var result = _detector.DetectCorrespondences(email);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void DetectCorrespondences_BorderTopDivFormat_SplitsCorrectly()
    {
        var email = new EmailMessage
        {
            From = "sender@outlook.com",
            To = "recipient@test.com",
            Subject = "FW: Test",
            HtmlBody = @"
                <div class=""MsoNormal"">
                    <p>Forwarding this to you</p>
                </div>
                <div>
                    <div style=""border:none;border-top:solid #E1E1E1 1.0pt;padding:3.0pt 0cm 0cm 0cm"">
                        <p><b>From:</b> Original Sender</p>
                        <p><b>Sent:</b> Monday, January 1, 2024</p>
                        <p><b>To:</b> Recipient</p>
                        <p>Original message content</p>
                    </div>
                </div>",
            EmailType = EmailType.Outlook
        };

        var result = _detector.DetectCorrespondences(email);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].IsParent);
    }
}
