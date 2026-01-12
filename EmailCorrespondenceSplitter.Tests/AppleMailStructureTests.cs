using EmailCorrespondenceSplitter.Services;
using System.Text;
using Xunit.Abstractions;
using HtmlAgilityPack;

namespace EmailCorrespondenceSplitter.Tests;

/// <summary>
/// Tests to diagnose Apple Mail HTML structure and footer extraction
/// </summary>
public class AppleMailStructureTests
{
    private readonly ITestOutputHelper _output;

    public AppleMailStructureTests(ITestOutputHelper output)
    {
        _output = output;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public async Task DiagnoseEm5AppleMailStructure()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testEmailPath = "Assets/em5.msg";

        // Act
        var email = await emailParser.ParseAsync(testEmailPath);
        var correspondences = correspondenceDetector.DetectCorrespondences(email);

        _output.WriteLine("=== EXTRACTED CORRESPONDENCES ===");
        for (int i = 0; i < correspondences.Count; i++)
        {
            _output.WriteLine($"\n--- Correspondence {i + 1} ---");
            _output.WriteLine($"From: {correspondences[i].From}");
            _output.WriteLine($"IsParent: {correspondences[i].IsParent}");
            _output.WriteLine($"Text Content (first 800 chars):");
            _output.WriteLine(correspondences[i].TextContent.Substring(0, Math.Min(800, correspondences[i].TextContent.Length)));
            _output.WriteLine("...");
        }

        // Verify extraction is correct
        Assert.Equal(2, correspondences.Count);
        
        var firstCorrespondence = correspondences[0];
        var secondCorrespondence = correspondences[1];
        
        // First correspondence (Nikki's email) should contain:
        // - Her message body
        // - Her signature with company info
        Assert.Contains("Good morning", firstCorrespondence.TextContent);
        Assert.Contains("Best wishes", firstCorrespondence.TextContent);
        Assert.Contains("Nikki", firstCorrespondence.TextContent);
        Assert.Contains("Austin John Ltd", firstCorrespondence.TextContent);
        Assert.Contains("https://austinjohnltd.co.uk", firstCorrespondence.TextContent);
        
        // First correspondence should NOT contain Pete's or Jack's content
        // (Pete's "FYI" message is part of the quoted email, not Nikki's)
        Assert.DoesNotContain("From: Jack Lawrence", firstCorrespondence.TextContent);
        
        // Second correspondence (Pete's forwarded message) should contain:
        // - The quote header "On 14 Mar 2023..."
        // - Pete's "FYI" message
        // - Jack's original email
        Assert.Contains("On 14 Mar 2023", secondCorrespondence.TextContent);
        Assert.Contains("FYI", secondCorrespondence.TextContent);
        Assert.Contains("Pete Wigginton", secondCorrespondence.TextContent);
        Assert.Contains("+44 (0) 7976 302178", secondCorrespondence.TextContent);
        Assert.Contains("Jack Lawrence", secondCorrespondence.TextContent);
        
        _output.WriteLine("\n=== ALL CHECKS PASSED ===");
        _output.WriteLine("First correspondence correctly contains only Nikki's email and signature");
        _output.WriteLine("Second correspondence correctly contains Pete's forwarded message with Jack's email");
    }
}
