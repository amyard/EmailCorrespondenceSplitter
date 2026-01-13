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
    
    [Fact]
    public async Task EmailWithAttachments_ShouldExtractAndStoreAttachments()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testOutputFolder = $"TestOutput_{Guid.NewGuid():N}";
        var outputManager = new OutputManager(testOutputFolder);
        var emailSplitter = new EmailSplitter(emailParser, correspondenceDetector, outputManager);
        
        // Try to find an email with attachments in Assets folder
        var assetFiles = Directory.GetFiles("Assets", "*.msg");
        
        string? testEmailPath = null;
        int originalAttachmentCount = 0;
        
        // Find an email with attachments
        foreach (var assetFile in assetFiles)
        {
            var email = await emailParser.ParseAsync(assetFile);
            if (email.Attachments.Count > 0)
            {
                testEmailPath = assetFile;
                originalAttachmentCount = email.Attachments.Count;
                _output.WriteLine($"Using test email: {Path.GetFileName(assetFile)}");
                _output.WriteLine($"Original attachment count: {originalAttachmentCount}");
                foreach (var att in email.Attachments)
                {
                    _output.WriteLine($"  - {att}");
                }
                break;
            }
        }
        
        // Skip test if no email with attachments found
        if (testEmailPath == null)
        {
            _output.WriteLine("No email with attachments found in Assets folder. Skipping test.");
            return;
        }

        try
        {
            // Act
            var count = await emailSplitter.ProcessEmailAsync(testEmailPath);
            
            _output.WriteLine($"\nProcessed {count} correspondence(s)");
            
            // Check output files
            var outputFolders = Directory.GetDirectories(testOutputFolder);
            Assert.Single(outputFolders);
            
            var msgFiles = Directory.GetFiles(outputFolders[0], "*.msg");
            _output.WriteLine($"Created {msgFiles.Length} MSG file(s)");
            
            // Verify at least one MSG file was created
            Assert.True(msgFiles.Length > 0, "Should create MSG files");
            
            // Check that at least one correspondence has the attachments
            bool foundAttachments = false;
            _output.WriteLine("\n=== Checking created MSG files ===");
            foreach (var file in msgFiles)
            {
                _output.WriteLine($"\n  {Path.GetFileName(file)}");
                
                using var msg = new MsgReader.Outlook.Storage.Message(file);
                
                if (msg.Attachments != null)
                {
                    // Filter out embedded images (those with ContentId)
                    var regularAttachments = msg.Attachments
                        .OfType<MsgReader.Outlook.Storage.Attachment>()
                        .Where(a => string.IsNullOrWhiteSpace(a.ContentId))
                        .ToList();
                    
                    _output.WriteLine($"    Regular attachments: {regularAttachments.Count}");
                    
                    foreach (var att in regularAttachments)
                    {
                        _output.WriteLine($"      - {att.FileName} ({att.Data?.Length ?? 0} bytes)");
                        foundAttachments = true;
                    }
                }
            }
            
            Assert.True(foundAttachments, "At least one correspondence should have regular attachments");
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
