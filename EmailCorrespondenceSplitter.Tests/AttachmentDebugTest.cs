using EmailCorrespondenceSplitter.Services;
using System.Text;
using Xunit.Abstractions;

namespace EmailCorrespondenceSplitter.Tests;

public class AttachmentDebugTest
{
    private readonly ITestOutputHelper _output;

    public AttachmentDebugTest(ITestOutputHelper output)
    {
        _output = output;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public async Task DebugEm3Attachments()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var testEmailPath = "Assets/em3.msg";

        // Act
        var email = await emailParser.ParseAsync(testEmailPath);
        
        _output.WriteLine($"=== EMAIL: {email.Subject} ===");
        _output.WriteLine($"From: {email.From}");
        _output.WriteLine($"To: {email.To}");
        _output.WriteLine("");
        
        _output.WriteLine($"Embedded Images: {email.EmbeddedImages.Count}");
        foreach (var img in email.EmbeddedImages)
        {
            _output.WriteLine($"  cid:{img.Key} ({img.Value.Length} bytes)");
        }
        
        _output.WriteLine("");
        _output.WriteLine($"Regular Attachments: {email.Attachments.Count}");
        foreach (var att in email.Attachments)
        {
            _output.WriteLine($"  {att}");
            if (email.AttachmentData.TryGetValue(att, out var data))
            {
                _output.WriteLine($"    Size: {data.Length} bytes");
            }
        }
        
        // Also check using MsgReader directly to see ALL attachments
        _output.WriteLine("");
        _output.WriteLine("=== RAW MSG READER OUTPUT ===");
        using var msg = new MsgReader.Outlook.Storage.Message(testEmailPath);
        
        if (msg.Attachments != null)
        {
            _output.WriteLine($"Total attachments in MSG: {msg.Attachments.Count}");
            foreach (var att in msg.Attachments)
            {
                if (att is MsgReader.Outlook.Storage.Attachment attachment)
                {
                    _output.WriteLine($"  Attachment: {attachment.FileName ?? "(no name)"}");
                    _output.WriteLine($"    ContentId: {attachment.ContentId ?? "(none)"}");
                    _output.WriteLine($"    Size: {attachment.Data?.Length ?? 0} bytes");
                    _output.WriteLine($"    IsInline: {!string.IsNullOrWhiteSpace(attachment.ContentId)}");
                    
                    // Check if referenced in HTML
                    if (!string.IsNullOrWhiteSpace(attachment.ContentId))
                    {
                        var cleanCid = attachment.ContentId.Trim('<', '>');
                        var isReferenced = msg.BodyHtml?.Contains($"cid:{cleanCid}") ?? false;
                        _output.WriteLine($"    Referenced in HTML: {isReferenced}");
                    }
                }
            }
        }
        
        // Verify the PDF is categorized as a regular attachment
        Assert.Contains("HAL15580_2 Hallen v Hallen.pdf", email.Attachments);
        Assert.Equal(1, email.Attachments.Count);
    }
    
    [Fact]
    public async Task Em3_ShouldIncludeAttachmentInOutputMsgFile()
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
            
            var msgFiles = Directory.GetFiles(outputFolders[0], "*.msg").OrderBy(f => f).ToArray();
            _output.WriteLine($"Created {msgFiles.Length} MSG file(s)");
            
            // The first correspondence should have the PDF attachment
            var firstMsgFile = msgFiles[0];
            _output.WriteLine($"\nChecking first correspondence: {Path.GetFileName(firstMsgFile)}");
            
            using var msg = new MsgReader.Outlook.Storage.Message(firstMsgFile);
            
            // Check attachments
            if (msg.Attachments != null)
            {
                _output.WriteLine($"Total attachments: {msg.Attachments.Count}");
                
                var regularAttachments = msg.Attachments
                    .OfType<MsgReader.Outlook.Storage.Attachment>()
                    .Where(a => string.IsNullOrWhiteSpace(a.ContentId))
                    .ToList();
                
                _output.WriteLine($"Regular attachments (non-embedded): {regularAttachments.Count}");
                
                foreach (var att in regularAttachments)
                {
                    _output.WriteLine($"  - {att.FileName} ({att.Data?.Length ?? 0} bytes)");
                }
                
                // Verify the PDF is included with correct filename
                var pdfAttachment = regularAttachments.FirstOrDefault(a => 
                    a.FileName != null && a.FileName.Contains("HAL15580_2"));
                    
                Assert.NotNull(pdfAttachment);
                Assert.Equal("HAL15580_2 Hallen v Hallen.pdf", pdfAttachment.FileName);
                _output.WriteLine($"\n? PDF attachment found with correct filename: {pdfAttachment.FileName}");
            }
            
            // Verify the body doesn't contain extra attachment information
            if (!string.IsNullOrEmpty(msg.BodyHtml))
            {
                _output.WriteLine("\nChecking HTML body for attachment info...");
                
                var hasExtraAttachmentInfo = msg.BodyHtml.Contains("?? Attachments") || 
                                            msg.BodyHtml.Contains("Attachments (1):");
                                       
                _output.WriteLine($"HTML contains extra attachment info: {hasExtraAttachmentInfo}");
                
                Assert.False(hasExtraAttachmentInfo, "Body should NOT contain extra attachment information");
                _output.WriteLine("? Body content is original without extra attachment info");
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
