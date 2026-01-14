using System.Text;
using EmailCorrespondenceSplitter.Pdf.Console.Models;
using EmailCorrespondenceSplitter.Pdf.Console.Services;
using HtmlAgilityPack;

// Register code pages for proper encoding support (required for MsgReader)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Console.WriteLine("===========================================");
Console.WriteLine("   Email Correspondence Splitter");
Console.WriteLine("===========================================\n");

// Get the Assets directory (should be copied to output during build)
var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
var outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output");

Console.WriteLine($"Assets directory: {assetsDirectory}");
Console.WriteLine($"Output directory: {outputDirectory}\n");

// Create and run the email splitter
var splitter = new EmailSplitter(outputDirectory);
await splitter.ProcessDirectoryAsync(assetsDirectory);

// Debug: Check the second correspondence for em1
var em1OutputDir = Directory.GetDirectories(outputDirectory).OrderByDescending(d => Directory.GetCreationTime(d)).FirstOrDefault(d => d.Contains("em1"));
if (em1OutputDir != null)
{
    var correspondenceFile = Directory.GetFiles(em1OutputDir, "*02_correspondence*").FirstOrDefault();
    if (correspondenceFile != null)
    {
        Console.WriteLine($"\n=== em1: Second correspondence ===");
        using var msg = new MsgReader.Outlook.Storage.Message(correspondenceFile);
        Console.WriteLine($"From: {msg.Sender?.DisplayName} <{msg.Sender?.Email}>");
        Console.WriteLine($"To: {string.Join("; ", msg.Recipients?.Select(r => $"{r.DisplayName} <{r.Email}>") ?? [])}");
        Console.WriteLine($"Subject: {msg.Subject}");
        Console.WriteLine($"SentOn: {msg.SentOn}");
        
        var htmlBody = msg.BodyHtml ?? "";
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlBody);
        var textContent = doc.DocumentNode.InnerText.Trim();
        
        Console.WriteLine($"Body (first 200 chars): {textContent.Substring(0, Math.Min(200, textContent.Length))}");
    }
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();