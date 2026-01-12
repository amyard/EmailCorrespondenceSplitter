using EmailCorrespondenceSplitter.Services;
using System.Text;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace EmailCorrespondenceSplitter.Tests;

/// <summary>
/// Diagnostic tests to help debug correspondence extraction issues
/// </summary>
public class DiagnosticTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public async Task DiagnoseEm6Structure()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testEmailPath = "Assets/em6.msg";

        // Act
        var email = await emailParser.ParseAsync(testEmailPath);
        
        // Output email details
        _output.WriteLine("=== EMAIL DETAILS ===");
        _output.WriteLine($"Email Type: {email.EmailType}");
        _output.WriteLine($"From: {email.From}");
        _output.WriteLine($"To: {email.To}");
        _output.WriteLine($"Subject: {email.Subject}");
        _output.WriteLine($"Sent On: {email.SentOn}");
        _output.WriteLine("");
        
        // Output full HTML Body for analysis
        _output.WriteLine("=== FULL HTML BODY ===");
        _output.WriteLine(email.HtmlBody);
        _output.WriteLine("");
        _output.WriteLine("=== END HTML BODY ===");
        _output.WriteLine("");
        
        // Check for common separators
        _output.WriteLine("=== SEPARATOR ANALYSIS ===");
        _output.WriteLine($"Contains <hr>: {email.HtmlBody?.Contains("<hr") ?? false}");
        _output.WriteLine($"Count of <hr>: {Regex.Matches(email.HtmlBody ?? "", "<hr", RegexOptions.IgnoreCase).Count}");
        _output.WriteLine($"Contains 'From:': {email.HtmlBody?.Contains("From:") ?? false}");
        _output.WriteLine($"Count of 'From:': {Regex.Matches(email.HtmlBody ?? "", "From:", RegexOptions.IgnoreCase).Count}");
        _output.WriteLine($"Contains '<b>From:</b>': {email.HtmlBody?.Contains("<b>From:</b>") ?? false}");
        _output.WriteLine($"Contains '<strong>From:</strong>': {email.HtmlBody?.Contains("<strong>From:</strong>") ?? false}");
        _output.WriteLine($"Contains 'border-top': {email.HtmlBody?.Contains("border-top") ?? false}");
        _output.WriteLine($"Contains 'Original Message': {email.HtmlBody?.Contains("Original Message") ?? false}");
        _output.WriteLine("");
        
        // Try to extract correspondences
        var correspondences = correspondenceDetector.DetectCorrespondences(email);
        
        _output.WriteLine("=== EXTRACTED CORRESPONDENCES ===");
        _output.WriteLine($"Total Count: {correspondences.Count}");
        
        foreach (var correspondence in correspondences)
        {
            _output.WriteLine("");
            _output.WriteLine($"--- Correspondence {correspondence.Index} ---");
            _output.WriteLine($"From: {correspondence.From}");
            _output.WriteLine($"To: {correspondence.To}");
            _output.WriteLine($"Sent On: {correspondence.SentOn}");
            _output.WriteLine($"IsParent: {correspondence.IsParent}");
            int contentLength = correspondence.HtmlContent?.Length ?? 0;
            _output.WriteLine($"HTML Content Length: {contentLength}");
            
            int previewLength = correspondence.HtmlContent != null ? Math.Min(500, correspondence.HtmlContent.Length) : 0;
            string preview = correspondence.HtmlContent != null && previewLength > 0 
                ? correspondence.HtmlContent.Substring(0, previewLength) 
                : "(empty)";
            _output.WriteLine($"HTML Content Preview: {preview}...");
        }
    }

    [Fact]
    public async Task SaveEm6HtmlToFile()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var testEmailPath = "Assets/em6.msg";

        // Act
        var email = await emailParser.ParseAsync(testEmailPath);
        
        // Save HTML to file for inspection
        var outputPath = "em6_html_output.html";
        await File.WriteAllTextAsync(outputPath, email.HtmlBody);
        
        _output.WriteLine($"HTML saved to: {Path.GetFullPath(outputPath)}");
        _output.WriteLine($"Email Type: {email.EmailType}");
        _output.WriteLine($"HTML Body Length: {email.HtmlBody?.Length}");
        
        // Parse HTML and find HR tags
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        var hrs = doc.DocumentNode.SelectNodes("//hr");
        _output.WriteLine($"Found {hrs?.Count ?? 0} <hr> tags");
        
        if (hrs != null)
        {
            for (int i = 0; i < hrs.Count; i++)
            {
                var hr = hrs[i];
                _output.WriteLine($"\nHR #{i + 1}:");
                _output.WriteLine($"  Parent: {hr.ParentNode?.Name}");
                _output.WriteLine($"  Previous Sibling: {hr.PreviousSibling?.Name} (text length: {hr.PreviousSibling?.InnerText?.Length})");
                _output.WriteLine($"  Next Sibling: {hr.NextSibling?.Name} (text length: {hr.NextSibling?.InnerText?.Length})");
                
                // Count siblings after this HR
                int siblingCount = 0;
                var sibling = hr.NextSibling;
                while (sibling != null)
                {
                    if (sibling.Name == "hr") break;
                    siblingCount++;
                    sibling = sibling.NextSibling;
                }
                _output.WriteLine($"  Siblings until next HR (or end): {siblingCount}");
            }
        }
    }
}
