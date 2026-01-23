using System.Text;
using System.Text.RegularExpressions;
using EmailCorrespondenceSplitter.Pdf.Console.Models;
using iText.Html2pdf;
using iText.Html2pdf.Resolver.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout.Font;
using Path = System.IO.Path;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Manages output folders and file operations for saving correspondences as PDF files
/// </summary>
public class PdfOutputManager
{
    private readonly string _outputBaseFolder;

    public PdfOutputManager(string outputBaseFolder)
    {
        _outputBaseFolder = outputBaseFolder;

        if (!Directory.Exists(_outputBaseFolder))
        {
            Directory.CreateDirectory(_outputBaseFolder);
        }
    }

    /// <summary>
    /// Create a folder for an email based on its filename
    /// </summary>
    public string CreateEmailFolder(string emailFilePath)
    {
        var emailFileName = Path.GetFileNameWithoutExtension(emailFilePath);
        var folderName = SanitizeFileName(emailFileName);

        var folderPath = Path.Combine(_outputBaseFolder, folderName);

        // If folder exists, append a number
        if (Directory.Exists(folderPath))
        {
            int counter = 1;
            while (Directory.Exists($"{folderPath}_{counter}"))
            {
                counter++;
            }
            folderPath = $"{folderPath}_{counter}";
        }

        Directory.CreateDirectory(folderPath);

        return folderPath;
    }

    /// <summary>
    /// Copy the original parent email to the output folder
    /// </summary>
    public void CopyParentEmail(string sourceFilePath, string outputFolder)
    {
        var fileName = $"00_parent_{Path.GetFileName(sourceFilePath)}";
        var destinationPath = Path.Combine(outputFolder, fileName);
        File.Copy(sourceFilePath, destinationPath, overwrite: true);
    }

    /// <summary>
    /// Save a correspondence as a PDF file
    /// </summary>
    public async Task SaveCorrespondenceAsync(Correspondence correspondence, string outputFolder)
    {
        var fromName = ExtractNameFromAddress(correspondence.From);
        var fileName = $"{(correspondence.Index + 1):D2}_correspondence_{SanitizeFileName(fromName)}.pdf";
        var filePath = Path.Combine(outputFolder, fileName);

        await Task.Run(() =>
        {
            try
            {
                // Build the complete HTML document
                var htmlContent = BuildCompleteHtmlDocument(correspondence);

                // Create a temporary directory for embedded images
                var tempDir = Path.Combine(Path.GetTempPath(), $"EmailSplitter_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Save embedded images to temp directory and update HTML references
                    htmlContent = ProcessEmbeddedImages(htmlContent, correspondence.EmbeddedImages, tempDir);

                    // Convert HTML to PDF
                    ConvertHtmlToPdf(htmlContent, filePath, tempDir);
                    
                    // Verify PDF was created with content
                    var fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists || fileInfo.Length == 0)
                    {
                        throw new InvalidOperationException("PDF file was not created or is empty");
                    }
                }
                finally
                {
                    // Clean up temp directory
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                    catch { /* Ignore cleanup errors */ }
                }

                // Save attachments in the same output folder (not a subfolder)
                if (correspondence.Attachments.Count > 0)
                {
                    SaveAttachments(correspondence.Attachments, outputFolder, correspondence.Index + 1);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"  Warning: Could not save PDF for correspondence {correspondence.Index + 1}: {ex.Message}");
                System.Console.WriteLine($"    Stack trace: {ex.StackTrace}");
                
                // Fallback: save as HTML file
                try
                {
                    var htmlFileName = $"{(correspondence.Index + 1):D2}_correspondence_{SanitizeFileName(fromName)}.html";
                    var htmlFilePath = Path.Combine(outputFolder, htmlFileName);
                    var htmlContent = BuildCompleteHtmlDocument(correspondence);
                    File.WriteAllText(htmlFilePath, htmlContent, Encoding.UTF8);
                    System.Console.WriteLine($"  Saved as HTML fallback: {htmlFileName}");
                }
                catch (Exception htmlEx)
                {
                    System.Console.WriteLine($"  Warning: Could not save HTML fallback: {htmlEx.Message}");
                }
            }
        });
    }

    /// <summary>
    /// Build a complete HTML document with proper structure and email header
    /// Header is only added for the first correspondence (parent email)
    /// </summary>
    private string BuildCompleteHtmlDocument(Correspondence correspondence)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<style>");
        sb.AppendLine(@"
            body { 
                font-family: Helvetica, Arial, sans-serif; 
                font-size: 11pt; 
                line-height: 1.4;
                margin: 20px;
                color: #000000;
            }
            .email-header {
                padding: 0;
                margin-bottom: 20px;
                border-bottom: 1px solid #cccccc;
                padding-bottom: 10px;
            }
            .email-header p {
                margin: 3px 0;
            }
            .label {
                font-weight: bold;
            }
            .email-body {
                padding: 10px 0;
            }
            img {
                max-width: 100%;
            }
            table {
                border-collapse: collapse;
            }
            p {
                margin: 0 0 10px 0;
            }
            strong, b {
                font-weight: bold;
            }
            em, i {
                font-style: italic;
            }
            .embedded-images {
                margin-top: 20px;
                padding-top: 10px;
                border-top: 1px solid #eeeeee;
            }
            /* Header styles */
            h1 {
                font-size: 24pt;
                font-weight: bold;
                margin: 20px 0 10px 0;
                color: #000000;
            }
            h2 {
                font-size: 20pt;
                font-weight: bold;
                margin: 18px 0 9px 0;
                color: #000000;
            }
            h3 {
                font-size: 16pt;
                font-weight: bold;
                margin: 16px 0 8px 0;
                color: #000000;
            }
            h4 {
                font-size: 14pt;
                font-weight: bold;
                margin: 14px 0 7px 0;
                color: #000000;
            }
            h5 {
                font-size: 12pt;
                font-weight: bold;
                margin: 12px 0 6px 0;
                color: #000000;
            }
            h6 {
                font-size: 11pt;
                font-weight: bold;
                margin: 10px 0 5px 0;
                color: #000000;
            }
            /* List styles */
            ul, ol {
                margin: 10px 0;
                padding-left: 30px;
            }
            ul {
                list-style-type: disc;
            }
            ol {
                list-style-type: decimal;
            }
            li {
                margin: 5px 0;
                line-height: 1.4;
            }
            ul ul, ol ol, ul ol, ol ul {
                margin: 5px 0;
            }
            /* Blockquote for quoted content */
            blockquote {
                margin: 10px 0 10px 20px;
                padding-left: 15px;
                border-left: 3px solid #cccccc;
                color: #555555;
            }
            /* Pre-formatted text */
            pre, code {
                font-family: 'Courier New', Courier, monospace;
                background-color: #f5f5f5;
                padding: 2px 5px;
            }
            pre {
                padding: 10px;
                margin: 10px 0;
                overflow-x: auto;
            }
        ");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Add email header ONLY for the first correspondence (parent email)
        if (correspondence.IsParent || correspondence.Index == 0)
        {
            sb.AppendLine("<div class=\"email-header\">");
            sb.AppendLine($"<p><span class=\"label\">From:</span> {System.Net.WebUtility.HtmlEncode(correspondence.From)}</p>");
            
            if (!string.IsNullOrWhiteSpace(correspondence.To))
            {
                sb.AppendLine($"<p><span class=\"label\">To:</span> {System.Net.WebUtility.HtmlEncode(correspondence.To)}</p>");
            }
            
            if (!string.IsNullOrWhiteSpace(correspondence.Cc))
            {
                sb.AppendLine($"<p><span class=\"label\">Cc:</span> {System.Net.WebUtility.HtmlEncode(correspondence.Cc)}</p>");
            }
            
            if (correspondence.SentOn.HasValue)
            {
                sb.AppendLine($"<p><span class=\"label\">Date:</span> {correspondence.SentOn.Value:f}</p>");
            }
            
            if (!string.IsNullOrWhiteSpace(correspondence.Subject))
            {
                sb.AppendLine($"<p><span class=\"label\">Subject:</span> {System.Net.WebUtility.HtmlEncode(correspondence.Subject)}</p>");
            }
            
            sb.AppendLine("</div>");
        }

        // Add email body
        sb.AppendLine("<div class=\"email-body\">");
        
        // Get the HTML content
        var bodyHtml = correspondence.HtmlContent;
        
        // If the content is a full HTML document, extract just the body
        bodyHtml = ExtractBodyContent(bodyHtml);
        
        // Clean HTML for better PDF compatibility
        bodyHtml = CleanHtmlForPdf(bodyHtml);
        
        sb.AppendLine(bodyHtml);
        sb.AppendLine("</div>");
        
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Clean HTML content for better PDF compatibility
    /// </summary>
    private string CleanHtmlForPdf(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "<p>No content</p>";

        // Remove unsupported CSS properties and fix common issues
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
    /// Extract body content from a full HTML document
    /// </summary>
    private string ExtractBodyContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        // Check if it's a full HTML document
        var bodyMatch = Regex.Match(html, @"<body[^>]*>(.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (bodyMatch.Success)
        {
            return bodyMatch.Groups[1].Value;
        }

        return html;
    }

    /// <summary>
    /// Remove email header (From, To, Subject, etc.) from the HTML body
    /// since we're adding our own styled header
    /// </summary>
    private string RemoveEmailHeaderFromBody(string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return htmlContent;

        // Pattern to find the email header block
        // This typically starts with a bold "From:" and ends after "Subject:"
        var patterns = new[]
        {
            // Pattern for div/p wrapped headers
            @"<(?:div|p)[^>]*>\s*(?:<(?:b|strong|span)[^>]*>)?\s*(?:From|Von|De|Da|Od):\s*.*?(?:Subject|Betreff|Objet|Asunto|Oggetto):\s*[^<]*(?:</(?:span|b|strong)>)?\s*</(?:div|p)>",
            // Pattern for border-top div headers (Outlook style)
            @"<div[^>]*style=[^>]*border-top[^>]*>.*?</div>",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(htmlContent, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && match.Index < 500) // Only remove if it's near the beginning
            {
                htmlContent = htmlContent.Substring(0, match.Index) + htmlContent.Substring(match.Index + match.Length);
                break;
            }
        }

        return htmlContent.TrimStart();
    }

    /// <summary>
    /// Process embedded images by saving them to temp directory and updating CID references
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

                // Replace cid: references with file path using file:// protocol
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
                System.Console.WriteLine($"  Warning: Could not process embedded image cid:{contentId}: {ex.Message}");
            }
        }

        return htmlContent;
    }

    /// <summary>
    /// Convert HTML content to PDF using iText7 pdfhtml
    /// </summary>
    private void ConvertHtmlToPdf(string htmlContent, string outputPath, string baseUri)
    {
        // Create converter properties
        var converterProperties = new ConverterProperties();
        
        // Set base URI for resolving relative paths (like images)
        converterProperties.SetBaseUri(baseUri + Path.DirectorySeparatorChar);
        
        // Set up font provider with standard fonts
        var fontProvider = new DefaultFontProvider(true, true, true);
        converterProperties.SetFontProvider(fontProvider);

        // Use the simplest overload: convert HTML string directly to a file
        // This is the most reliable approach
        using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            // Convert HTML string to byte array
            var htmlBytes = Encoding.UTF8.GetBytes(htmlContent);
            
            using (var htmlInputStream = new MemoryStream(htmlBytes))
            {
                // Use the direct file conversion method
                HtmlConverter.ConvertToPdf(htmlInputStream, fileStream, converterProperties);
            }
        }
    }

    /// <summary>
    /// Save attachments to the same output folder as PDFs
    /// </summary>
    private void SaveAttachments(Dictionary<string, byte[]> attachments, string outputFolder, int correspondenceIndex)
    {
        foreach (var attachment in attachments)
        {
            var originalFileName = attachment.Key;
            var fileData = attachment.Value;

            try
            {
                // Prefix with correspondence index to avoid name conflicts
                var fileName = $"{correspondenceIndex:D2}_attachment_{SanitizeFileName(originalFileName)}";
                var filePath = Path.Combine(outputFolder, fileName);

                // Handle duplicate filenames
                if (File.Exists(filePath))
                {
                    var baseName = Path.GetFileNameWithoutExtension(fileName);
                    var extension = Path.GetExtension(fileName);
                    int counter = 1;
                    while (File.Exists(filePath))
                    {
                        fileName = $"{baseName}_{counter}{extension}";
                        filePath = Path.Combine(outputFolder, fileName);
                        counter++;
                    }
                }

                File.WriteAllBytes(filePath, fileData);
                System.Console.WriteLine($"    Saved attachment: {fileName}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"  Warning: Could not save attachment {originalFileName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Extract the name part from an email address
    /// </summary>
    private string ExtractNameFromAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "Unknown";

        // Decode HTML entities
        address = System.Net.WebUtility.HtmlDecode(address);

        // Pattern: "Display Name <email@domain.com>"
        var match = Regex.Match(address, @"^(.+?)\s*<[^>]+>$");
        if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
        {
            return match.Groups[1].Value.Trim();
        }

        // Just return the email address part before @
        var atIndex = address.IndexOf('@');
        if (atIndex > 0)
        {
            return address.Substring(0, atIndex);
        }

        return address;
    }

    private string GetImageExtension(byte[] imageData)
    {
        if (imageData.Length < 4)
            return ".png";

        // PNG
        if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            return ".png";

        // JPEG
        if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            return ".jpg";

        // GIF
        if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x38)
            return ".gif";

        // BMP
        if (imageData[0] == 0x42 && imageData[1] == 0x4D)
            return ".bmp";

        // WebP
        if (imageData.Length >= 12 &&
            imageData[0] == 0x52 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x46 &&
            imageData[8] == 0x57 && imageData[9] == 0x45 && imageData[10] == 0x42 && imageData[11] == 0x50)
            return ".webp";

        return ".png";
    }

    private string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());

        // Also replace some characters that might cause issues
        sanitized = sanitized.Replace("@", "_at_").Replace("<", "").Replace(">", "");

        // Limit length
        if (sanitized.Length > 50)
        {
            sanitized = sanitized[..50];
        }

        return sanitized.Trim();
    }
}
