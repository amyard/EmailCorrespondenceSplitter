using EmailCorrespondenceSplitter.Services;
using System.Text;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace EmailCorrespondenceSplitter.Tests;

public class Em4DebugTest
{
    private readonly ITestOutputHelper _output;

    public Em4DebugTest(ITestOutputHelper output)
    {
        _output = output;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public async Task DebugEm4Structure()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testEmailPath = "Assets/em4.msg";

        // Act
        var email = await emailParser.ParseAsync(testEmailPath);
        
        _output.WriteLine($"=== EMAIL: {email.Subject} ===");
        _output.WriteLine($"From: {email.From}");
        _output.WriteLine($"To: {email.To}");
        _output.WriteLine($"Email Type: {email.EmailType}");
        _output.WriteLine("");
        
        // Count "From:" occurrences in HTML
        var fromPattern = @"From:\s*(.+?)(?:<br|</|$)";
        var matches = Regex.Matches(email.HtmlBody, fromPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        _output.WriteLine($"'From:' occurrences in HTML: {matches.Count}");
        
        for (int i = 0; i < Math.Min(matches.Count, 10); i++)
        {
            _output.WriteLine($"  {i + 1}. From: {matches[i].Groups[1].Value.Trim().Substring(0, Math.Min(50, matches[i].Groups[1].Value.Trim().Length))}...");
        }
        
        _output.WriteLine("");
        
        // Check for HR tags
        var hrPattern = @"<hr[^>]*>";
        var hrMatches = Regex.Matches(email.HtmlBody, hrPattern, RegexOptions.IgnoreCase);
        _output.WriteLine($"<hr> tags found: {hrMatches.Count}");
        
        // Check for blockquotes
        var blockquotePattern = @"<blockquote[^>]*>";
        var blockquoteMatches = Regex.Matches(email.HtmlBody, blockquotePattern, RegexOptions.IgnoreCase);
        _output.WriteLine($"<blockquote> tags found: {blockquoteMatches.Count}");
        
        _output.WriteLine("");
        
        // Extract correspondences
        var correspondences = correspondenceDetector.DetectCorrespondences(email);
        
        _output.WriteLine($"=== DETECTED CORRESPONDENCES: {correspondences.Count} ===");
        
        for (int i = 0; i < correspondences.Count; i++)
        {
            var corr = correspondences[i];
            _output.WriteLine($"\n--- Correspondence {i + 1} ---");
            _output.WriteLine($"From: {corr.From}");
            _output.WriteLine($"To: {corr.To}");
            _output.WriteLine($"IsParent: {corr.IsParent}");
            _output.WriteLine($"HTML Content Length: {corr.HtmlContent?.Length ?? 0}");
            
            // Preview first 200 chars
            if (!string.IsNullOrEmpty(corr.HtmlContent))
            {
                var preview = corr.HtmlContent.Length > 200 
                    ? corr.HtmlContent.Substring(0, 200) 
                    : corr.HtmlContent;
                _output.WriteLine($"Content preview: {preview}...");
            }
        }
        
        // Save HTML to file for inspection
        var outputPath = "em4_html_output.html";
        await File.WriteAllTextAsync(outputPath, email.HtmlBody);
        _output.WriteLine($"\nHTML saved to: {Path.GetFullPath(outputPath)}");
    }
}
