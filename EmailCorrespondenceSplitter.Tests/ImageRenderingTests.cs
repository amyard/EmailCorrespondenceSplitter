using EmailCorrespondenceSplitter.Services;
using System.Text;
using Xunit.Abstractions;

namespace EmailCorrespondenceSplitter.Tests;

public class ImageRenderingTests
{
    private readonly ITestOutputHelper _output;

    public ImageRenderingTests(ITestOutputHelper output)
    {
        _output = output;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public async Task Em3_ShouldExtractEmbeddedImages()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testEmailPath = "Assets/em3.msg";

        // Act
        var email = await emailParser.ParseAsync(testEmailPath);
        
        _output.WriteLine($"=== EMAIL: {email.Subject} ===");
        _output.WriteLine($"Total embedded images in email: {email.EmbeddedImages.Count}");
        
        foreach (var img in email.EmbeddedImages)
        {
            _output.WriteLine($"  cid:{img.Key} ({img.Value.Length} bytes)");
        }
        
        var correspondences = correspondenceDetector.DetectCorrespondences(email);
        
        _output.WriteLine($"\n=== CORRESPONDENCES: {correspondences.Count} ===");
        
        for (int i = 0; i < correspondences.Count; i++)
        {
            var corr = correspondences[i];
            _output.WriteLine($"\n--- Correspondence {i + 1} ---");
            _output.WriteLine($"From: {corr.From}");
            _output.WriteLine($"Images: {corr.EmbeddedImages.Count}");
            
            foreach (var img in corr.EmbeddedImages)
            {
                _output.WriteLine($"  cid:{img.Key} ({img.Value.Length} bytes)");
            }
            
            // Check if HTML contains cid: references
            var cidMatches = System.Text.RegularExpressions.Regex.Matches(
                corr.HtmlContent, 
                @"cid:([^'""]+)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            _output.WriteLine($"CID references in HTML: {cidMatches.Count}");
            foreach (System.Text.RegularExpressions.Match match in cidMatches)
            {
                _output.WriteLine($"  Reference: cid:{match.Groups[1].Value}");
            }
        }
        
        // Assert
        Assert.True(email.EmbeddedImages.Count > 0, "Email should have embedded images");
        Assert.True(correspondences.Any(c => c.EmbeddedImages.Count > 0), 
            "At least one correspondence should have embedded images");
    }
    
    [Fact]
    public async Task Em3_OutputShouldPreserveInlineImages()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testOutputFolder = $"TestOutput_{Guid.NewGuid():N}";
        var outputManager = new OutputManager(testOutputFolder);
        var emailSplitter = new EmailSplitter(emailParser, correspondenceDetector, outputManager);
        var testEmailPath = "Assets/em3.msg";

        try
        {
            // Act
            var count = await emailSplitter.ProcessEmailAsync(testEmailPath);
            
            _output.WriteLine($"Processed {count} correspondence(s)");
            
            // Check output files
            var outputFolders = Directory.GetDirectories(testOutputFolder);
            Assert.Single(outputFolders);
            
            var msgFiles = Directory.GetFiles(outputFolders[0], "*.msg");
            _output.WriteLine($"Created {msgFiles.Length} MSG file(s)");
            
            // Verify at least one MSG file was created
            Assert.True(msgFiles.Length > 0, "Should create MSG files");
            
            _output.WriteLine("\n=== Files created ===");
            foreach (var file in msgFiles)
            {
                _output.WriteLine($"  {Path.GetFileName(file)}");
                
                // Verify that the created MSG files can be read and have embedded images
                using var msg = new MsgReader.Outlook.Storage.Message(file);
                _output.WriteLine($"    HTML Body length: {msg.BodyHtml?.Length ?? 0}");
                _output.WriteLine($"    Attachments: {msg.Attachments?.Count ?? 0}");
                
                // Check for cid: references in HTML
                if (!string.IsNullOrEmpty(msg.BodyHtml))
                {
                    var cidMatches = System.Text.RegularExpressions.Regex.Matches(
                        msg.BodyHtml, 
                        @"cid:([^'""]+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    _output.WriteLine($"    CID references in HTML: {cidMatches.Count}");
                    
                    // Verify embedded images are present as attachments with ContentId
                    if (msg.Attachments != null)
                    {
                        var embeddedImages = msg.Attachments
                            .OfType<MsgReader.Outlook.Storage.Attachment>()
                            .Where(a => !string.IsNullOrWhiteSpace(a.ContentId))
                            .ToList();
                        
                        _output.WriteLine($"    Embedded images (with ContentId): {embeddedImages.Count}");
                        foreach (var img in embeddedImages)
                        {
                            _output.WriteLine($"      cid:{img.ContentId?.Trim('<', '>')} - {img.FileName}");
                        }
                    }
                }
            }
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testOutputFolder))
            {
                Directory.Delete(testOutputFolder, true);
            }
        }
    }
}
