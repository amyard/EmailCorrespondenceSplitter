using System.Text;
using System.Text.RegularExpressions;
using EmailCorrespondenceSplitter.Pdf.Console.Models;
using iText.Html2pdf;
using iText.Html2pdf.Resolver.Font;
using iText.Kernel.Pdf;
using iText.Layout.Font;
using Path = System.IO.Path;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Creates PDF files for each correspondence using the same detection as PdfCorrespondenceDetector.
/// Uses iText7 to generate styled PDFs from the extracted correspondence content.
/// This ensures the same number of correspondences as the OLD method.
/// </summary>
public class PdfDirectCorrespondenceSplitter
{
    private readonly PdfCorrespondenceDetector _detector;

    public PdfDirectCorrespondenceSplitter()
    {
        _detector = new PdfCorrespondenceDetector();
    }

    /// <summary>
    /// Split a PDF file into separate correspondence PDF files based on "From:" sections
    /// Uses the SAME detection logic as PdfCorrespondenceDetector for identical results.
    /// </summary>
    /// <param name="inputPdfPath">Path to the input PDF file</param>
    /// <param name="outputFolder">Folder to save the split PDF files</param>
    /// <param name="parsedEmail">The already-parsed email from PdfEmailParser</param>
    /// <returns>Number of correspondences found and saved</returns>
    public async Task<int> SplitPdfByCorrespondencesAsync(string inputPdfPath, string outputFolder, EmailMessage parsedEmail)
    {
        return await Task.Run(() => SplitPdfByCorrespondences(inputPdfPath, outputFolder, parsedEmail));
    }

    /// <summary>
    /// Split PDF by correspondences (synchronous)
    /// </summary>
    private int SplitPdfByCorrespondences(string inputPdfPath, string outputFolder, EmailMessage parsedEmail)
    {
        try
        {
            // Use the SAME detector as the OLD method to get identical correspondences
            var correspondences = _detector.DetectCorrespondences(parsedEmail);

            if (correspondences.Count <= 1)
            {
                System.Console.WriteLine("  No multiple correspondences found");
                return 0;
            }

            System.Console.WriteLine($"  Found {correspondences.Count} correspondence(s) using same detector as OLD method");

            // Create PDF for each correspondence
            for (int i = 0; i < correspondences.Count; i++)
            {
                var correspondence = correspondences[i];
                CreateCorrespondencePdf(correspondence, outputFolder, i);
            }

            return correspondences.Count;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"  Error splitting PDF: {ex.Message}");
            System.Console.WriteLine($"  Stack trace: {ex.StackTrace}");
            return 0;
        }
    }

    /// <summary>
    /// Create a PDF file for a single correspondence with full styling
    /// </summary>
    private void CreateCorrespondencePdf(Correspondence correspondence, string outputFolder, int index)
    {
        try
        {
            var fileName = $"{(index + 1):D2}_correspondence.pdf";
            var outputPath = Path.Combine(outputFolder, fileName);

            // Build HTML content with styling
            var htmlContent = BuildStyledHtmlDocument(correspondence);

            // Create temp directory for images
            var tempDir = Path.Combine(Path.GetTempPath(), $"PdfSplitter_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Process embedded images
                htmlContent = ProcessEmbeddedImages(htmlContent, correspondence.EmbeddedImages, tempDir);

                // Convert HTML to PDF using iText7
                ConvertHtmlToPdf(htmlContent, outputPath, tempDir);

                var fromPreview = correspondence.From.Length > 30 
                    ? correspondence.From.Substring(0, 30) + "..." 
                    : correspondence.From;
                System.Console.WriteLine($"  Saved correspondence {index + 1}: {fileName} (From: {fromPreview})");
            }
            finally
            {
                // Cleanup temp directory
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { /* Ignore cleanup errors */ }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"  Error creating PDF for correspondence {index + 1}: {ex.Message}");
        }
    }

    /// <summary>
    /// Build a complete styled HTML document for a correspondence
    /// </summary>
    private string BuildStyledHtmlDocument(Correspondence correspondence)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<style>");
        sb.AppendLine(@"
            body { 
                font-family: Arial, Helvetica, sans-serif; 
                font-size: 11pt; 
                line-height: 1.5;
                margin: 25px;
                color: #000000;
            }
            .email-header {
                background-color: #f5f5f5;
                padding: 15px;
                margin-bottom: 20px;
                border-left: 4px solid #0078d4;
            }
            .email-header p {
                margin: 5px 0;
            }
            .header-label {
                font-weight: bold;
                color: #333;
                display: inline-block;
                min-width: 80px;
            }
            .email-body {
                padding: 10px 0;
            }
            .email-body p {
                margin: 0 0 10px 0;
            }
            img {
                max-width: 100%;
                height: auto;
            }
            .embedded-images {
                margin-top: 20px;
                padding-top: 10px;
                border-top: 1px solid #eee;
            }
        ");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Email header section
        sb.AppendLine("<div class=\"email-header\">");
        sb.AppendLine($"<p><span class=\"header-label\">From:</span> {System.Net.WebUtility.HtmlEncode(correspondence.From)}</p>");

        if (!string.IsNullOrWhiteSpace(correspondence.To))
        {
            sb.AppendLine($"<p><span class=\"header-label\">To:</span> {System.Net.WebUtility.HtmlEncode(correspondence.To)}</p>");
        }

        if (!string.IsNullOrWhiteSpace(correspondence.Cc))
        {
            sb.AppendLine($"<p><span class=\"header-label\">Cc:</span> {System.Net.WebUtility.HtmlEncode(correspondence.Cc)}</p>");
        }

        if (correspondence.SentOn.HasValue)
        {
            sb.AppendLine($"<p><span class=\"header-label\">Date:</span> {correspondence.SentOn.Value:f}</p>");
        }

        if (!string.IsNullOrWhiteSpace(correspondence.Subject))
        {
            sb.AppendLine($"<p><span class=\"header-label\">Subject:</span> {System.Net.WebUtility.HtmlEncode(correspondence.Subject)}</p>");
        }

        sb.AppendLine("</div>");

        // Email body section
        sb.AppendLine("<div class=\"email-body\">");

        var bodyHtml = correspondence.HtmlContent;

        // If it's a full HTML document, extract just the body
        if (!string.IsNullOrWhiteSpace(bodyHtml))
        {
            bodyHtml = ExtractBodyContent(bodyHtml);
            bodyHtml = CleanHtmlForPdf(bodyHtml);
            sb.AppendLine(bodyHtml);
        }
        else if (!string.IsNullOrWhiteSpace(correspondence.TextContent))
        {
            // Convert plain text to HTML
            var textHtml = ConvertTextToHtml(correspondence.TextContent);
            sb.AppendLine(textHtml);
        }
        else
        {
            sb.AppendLine("<p>No content</p>");
        }

        sb.AppendLine("</div>");

        // Add embedded images that aren't already in the content
        if (correspondence.EmbeddedImages.Count > 0)
        {
            sb.AppendLine("<div class=\"embedded-images\">");
            foreach (var image in correspondence.EmbeddedImages)
            {
                // Check if image is already referenced in content
                if (!bodyHtml.Contains($"cid:{image.Key}", StringComparison.OrdinalIgnoreCase))
                {
                    var base64Data = Convert.ToBase64String(image.Value);
                    var mimeType = GetMimeType(image.Value);
                    sb.AppendLine($"<p><img src=\"data:{mimeType};base64,{base64Data}\" alt=\"Embedded Image\"/></p>");
                }
            }
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Extract body content from a full HTML document
    /// </summary>
    private string ExtractBodyContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        var bodyMatch = Regex.Match(html, @"<body[^>]*>(.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (bodyMatch.Success)
        {
            return bodyMatch.Groups[1].Value;
        }

        return html;
    }

    /// <summary>
    /// Clean HTML for better PDF compatibility
    /// </summary>
    private string CleanHtmlForPdf(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "<p>No content</p>";

        // Remove MSO-specific styles
        html = Regex.Replace(html, @"mso-[^;""']+[;]?", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"class=""Mso[^""]*""", "", RegexOptions.IgnoreCase);

        // Remove conditional comments
        html = Regex.Replace(html, @"<!--\[if[^\]]*\]>.*?<!\[endif\]-->", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<!--.*?-->", "", RegexOptions.Singleline);

        // Fix empty paragraphs
        html = Regex.Replace(html, @"<p[^>]*>\s*&nbsp;\s*</p>", "<br/>", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<p[^>]*>\s*</p>", "", RegexOptions.IgnoreCase);

        return html;
    }

    /// <summary>
    /// Convert plain text to HTML
    /// </summary>
    private string ConvertTextToHtml(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "<p>No content</p>";

        var html = System.Net.WebUtility.HtmlEncode(text);
        html = html.Replace("\r\n", "\n").Replace("\r", "\n");

        var paragraphs = Regex.Split(html, @"\n\s*\n");
        var sb = new StringBuilder();

        foreach (var para in paragraphs)
        {
            if (!string.IsNullOrWhiteSpace(para))
            {
                var content = para.Trim().Replace("\n", "<br/>");
                sb.AppendLine($"<p>{content}</p>");
            }
        }

        return sb.Length > 0 ? sb.ToString() : "<p>No content</p>";
    }

    /// <summary>
    /// Process embedded images by saving to temp directory
    /// </summary>
    private string ProcessEmbeddedImages(string htmlContent, Dictionary<string, byte[]> embeddedImages, string tempDir)
    {
        if (embeddedImages.Count == 0)
            return htmlContent;

        foreach (var imageEntry in embeddedImages)
        {
            var contentId = imageEntry.Key;
            var imageData = imageEntry.Value;

            var extension = GetImageExtension(imageData);
            var imageName = $"image_{SanitizeFileName(contentId)}{extension}";
            var imagePath = Path.Combine(tempDir, imageName);

            try
            {
                File.WriteAllBytes(imagePath, imageData);

                var fileUri = new Uri(imagePath).AbsoluteUri;

                htmlContent = Regex.Replace(
                    htmlContent,
                    $@"(src|background)\s*=\s*['""]cid:{Regex.Escape(contentId)}['""]",
                    $@"$1=""{fileUri}""",
                    RegexOptions.IgnoreCase
                );
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"    Warning: Could not process image {contentId}: {ex.Message}");
            }
        }

        return htmlContent;
    }

    /// <summary>
    /// Convert HTML to PDF using iText7
    /// </summary>
    private void ConvertHtmlToPdf(string htmlContent, string outputPath, string baseUri)
    {
        var converterProperties = new ConverterProperties();
        converterProperties.SetBaseUri(baseUri + Path.DirectorySeparatorChar);

        var fontProvider = new DefaultFontProvider(true, true, true);
        converterProperties.SetFontProvider(fontProvider);

        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var htmlBytes = Encoding.UTF8.GetBytes(htmlContent);

        using var htmlInputStream = new MemoryStream(htmlBytes);
        HtmlConverter.ConvertToPdf(htmlInputStream, fileStream, converterProperties);
    }

    /// <summary>
    /// Get MIME type from image bytes
    /// </summary>
    private string GetMimeType(byte[] imageData)
    {
        if (imageData.Length < 4) return "image/png";

        if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            return "image/png";
        if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            return "image/jpeg";
        if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x38)
            return "image/gif";
        if (imageData[0] == 0x42 && imageData[1] == 0x4D)
            return "image/bmp";

        return "image/png";
    }

    /// <summary>
    /// Get image file extension from bytes
    /// </summary>
    private string GetImageExtension(byte[] imageData)
    {
        if (imageData.Length < 4) return ".png";

        if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            return ".png";
        if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            return ".jpg";
        if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x38)
            return ".gif";
        if (imageData[0] == 0x42 && imageData[1] == 0x4D)
            return ".bmp";

        return ".png";
    }

    /// <summary>
    /// Sanitize filename
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        sanitized = sanitized.Replace("@", "_at_").Replace("<", "").Replace(">", "");

        if (sanitized.Length > 50)
            sanitized = sanitized[..50];

        return sanitized.Trim();
    }
}
