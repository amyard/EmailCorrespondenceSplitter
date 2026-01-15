using System.Text;
using System.Text.RegularExpressions;
using EmailCorrespondenceSplitter.Pdf.Console.Models;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Detects and extracts individual correspondences from PDF email content.
/// Splits by "From:" patterns in the text, preserving original styles.
/// </summary>
public class PdfCorrespondenceDetector
{
    // Multi-language patterns for "From:" field
    private static readonly string[] FromPatterns =
    [
        "From",      // English
        "Von",       // German
        "De",        // French, Spanish, Portuguese
        "Da",        // Italian
        "??",        // Russian
        "Od",        // Polish, Czech
        "Från",      // Swedish
        "Fra",       // Norwegian, Danish
        "???",    // Japanese
        "????",   // Korean
        "???",    // Chinese Simplified
        "???",    // Chinese Traditional
    ];

    private static readonly string[] SentPatterns =
    [
        "Sent", "Date", "Gesendet", "Envoyé", "Enviado", "Inviato", "??????????", "Wys?ano", "Skickat", "Sendt", "Datum"
    ];

    private static readonly string[] ToPatterns =
    [
        "To", "An", "À", "A", "????", "Do", "Till", "Til"
    ];

    private static readonly string[] SubjectPatterns =
    [
        "Subject", "Betreff", "Objet", "Asunto", "Oggetto", "????", "Temat", "Ämne", "Emne"
    ];

    private static readonly string[] CcPatterns =
    [
        "Cc", "CC", "Kopie", "Copie", "Copia", "?????", "Kopia"
    ];

    /// <summary>
    /// Detect correspondences in PDF email content by splitting on "From:" patterns
    /// </summary>
    public List<Correspondence> DetectCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var textContent = email.TextBody;
        var htmlContent = email.HtmlBody;

        if (string.IsNullOrWhiteSpace(textContent))
        {
            return [CreateSingleCorrespondence(email)];
        }

        // Build the split pattern for "From:" in multiple languages
        var fromPatternString = string.Join("|", FromPatterns.Select(Regex.Escape));
        var splitPattern = $@"(?=^\s*(?:{fromPatternString}):\s*.+$)";

        // Split the text content by "From:" lines and track positions
        var textSections = SplitWithPositions(textContent, splitPattern);

        if (textSections.Count <= 1)
        {
            return [CreateSingleCorrespondence(email)];
        }

        // Get page ranges if available
        var pageRanges = GetPageRanges(email);
        
        // Distribute images to sections based on page numbers and text positions
        var imageDistribution = DistributeImagesByPosition(email.EmbeddedImages, textSections, pageRanges);

        // Split HTML content if available
        var htmlSections = SplitHtmlContent(htmlContent, textSections.Count);

        for (int i = 0; i < textSections.Count; i++)
        {
            var section = textSections[i];
            var sectionText = section.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(sectionText))
                continue;

            var metadata = ExtractEmailMetadata(sectionText);
            
            // Get images for this correspondence
            var correspondenceImages = imageDistribution.ContainsKey(i) 
                ? imageDistribution[i] 
                : new Dictionary<string, byte[]>();
            
            // Build HTML content - use styled HTML section if available, otherwise convert text
            string sectionHtml;
            if (i < htmlSections.Count && !string.IsNullOrWhiteSpace(htmlSections[i]))
            {
                // Use the styled HTML and add images
                sectionHtml = htmlSections[i];
                if (correspondenceImages.Count > 0)
                {
                    sectionHtml = AddImagesToHtml(sectionHtml, correspondenceImages);
                }
            }
            else
            {
                // Fallback to converting text to HTML with images
                sectionHtml = ConvertTextToHtmlWithImages(sectionText, correspondenceImages);
            }

            correspondences.Add(new Correspondence
            {
                From = metadata.From ?? (i == 0 ? email.From : "Unknown"),
                To = metadata.To ?? (i == 0 ? email.To : string.Empty),
                Cc = metadata.Cc ?? string.Empty,
                SentOn = metadata.Date ?? (i == 0 ? email.SentOn : null),
                Subject = metadata.Subject ?? email.Subject,
                HtmlContent = sectionHtml,
                TextContent = sectionText,
                Index = i,
                IsParent = i == 0,
                EmbeddedImages = correspondenceImages,
                Attachments = i == 0 ? new Dictionary<string, byte[]>(email.AttachmentData) : []
            });
        }

        if (correspondences.Count == 0)
        {
            correspondences.Add(CreateSingleCorrespondence(email));
        }

        return correspondences;
    }

    /// <summary>
    /// Split HTML content into sections matching the text sections count
    /// </summary>
    private List<string> SplitHtmlContent(string htmlContent, int expectedSections)
    {
        var htmlSections = new List<string>();
        
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            // No HTML content - return empty list so we use text-to-HTML conversion for all sections
            for (int i = 0; i < expectedSections; i++)
            {
                htmlSections.Add(string.Empty);
            }
            return htmlSections;
        }

        // Try to split HTML by finding "From:" patterns
        var fromPatternString = string.Join("|", FromPatterns.Select(Regex.Escape));
        
        // Pattern to find paragraph containing "From:" - handles various HTML structures
        // Look for: <p>From: or <p><strong>From: or <p><span>From: etc.
        var htmlFromPattern = $@"<p[^>]*>(?:\s*<[^>]+>)*\s*(?:{fromPatternString}):\s*";
        
        var matches = Regex.Matches(htmlContent, htmlFromPattern, RegexOptions.IgnoreCase);
        
        if (matches.Count >= expectedSections)
        {
            // We have enough matches to split - each section starts at a "From:" match
            var splitPoints = new List<int>();
            
            // Take only the first expectedSections matches (one per section)
            for (int i = 0; i < expectedSections && i < matches.Count; i++)
            {
                splitPoints.Add(matches[i].Index);
            }
            
            for (int i = 0; i < splitPoints.Count; i++)
            {
                int start = splitPoints[i];
                int end = (i + 1 < splitPoints.Count) ? splitPoints[i + 1] : htmlContent.Length;
                htmlSections.Add(htmlContent.Substring(start, end - start));
            }
        }
        else
        {
            // Can't split HTML reliably - use text-to-HTML conversion for all sections
            // This ensures each correspondence gets only its own content
            for (int i = 0; i < expectedSections; i++)
            {
                htmlSections.Add(string.Empty);
            }
        }

        return htmlSections;
    }

    /// <summary>
    /// Add images to HTML content
    /// </summary>
    private string AddImagesToHtml(string html, Dictionary<string, byte[]> images)
    {
        if (images.Count == 0)
            return html;

        var sb = new StringBuilder(html);
        sb.AppendLine();
        sb.AppendLine("<div class=\"embedded-images\" style=\"margin-top: 20px;\">");
        foreach (var image in images)
        {
            var base64Data = Convert.ToBase64String(image.Value);
            var mimeType = GetMimeType(image.Value);
            sb.AppendLine($"<p><img src=\"data:{mimeType};base64,{base64Data}\" alt=\"Embedded Image\" style=\"max-width:100%;\"/></p>");
        }
        sb.AppendLine("</div>");
        
        return sb.ToString();
    }

    /// <summary>
    /// Split text by pattern and track start/end positions
    /// </summary>
    private List<(string Text, int StartIndex, int EndIndex)> SplitWithPositions(string text, string pattern)
    {
        var sections = new List<(string Text, int StartIndex, int EndIndex)>();
        var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        
        if (matches.Count == 0)
        {
            sections.Add((text, 0, text.Length));
            return sections;
        }

        var splitIndices = matches.Cast<Match>().Select(m => m.Index).ToList();
        
        for (int i = 0; i < splitIndices.Count; i++)
        {
            int start = splitIndices[i];
            int end = (i + 1 < splitIndices.Count) ? splitIndices[i + 1] : text.Length;
            var sectionText = text.Substring(start, end - start);
            
            if (!string.IsNullOrWhiteSpace(sectionText))
            {
                sections.Add((sectionText, start, end));
            }
        }

        return sections;
    }

    /// <summary>
    /// Get page ranges from email custom data
    /// </summary>
    private List<(int PageNumber, int StartIndex, int EndIndex)> GetPageRanges(EmailMessage email)
    {
        if (email.CustomData.TryGetValue("PageTextRanges", out var rangesObj) && 
            rangesObj is List<(int PageNumber, int StartIndex, int EndIndex)> ranges)
        {
            return ranges;
        }
        return [];
    }

    /// <summary>
    /// Distribute images to correspondences based on page numbers and text positions
    /// </summary>
    private Dictionary<int, Dictionary<string, byte[]>> DistributeImagesByPosition(
        Dictionary<string, byte[]> allImages,
        List<(string Text, int StartIndex, int EndIndex)> sections,
        List<(int PageNumber, int StartIndex, int EndIndex)> pageRanges)
    {
        var distribution = new Dictionary<int, Dictionary<string, byte[]>>();
        
        // Always initialize all sections
        for (int i = 0; i < sections.Count; i++)
        {
            distribution[i] = new Dictionary<string, byte[]>();
        }
        
        if (allImages.Count == 0)
            return distribution;

        foreach (var imageEntry in allImages)
        {
            var imageId = imageEntry.Key;
            var imageData = imageEntry.Value;
            
            // Parse page number from image ID (format: pdf_image_pX_iY)
            int imagePageNumber = 1;
            var pageMatch = Regex.Match(imageId, @"pdf_image_p(\d+)_i\d+");
            if (pageMatch.Success)
            {
                imagePageNumber = int.Parse(pageMatch.Groups[1].Value);
            }
            
            // Find which section this page belongs to
            int targetSection = FindSectionForPage(imagePageNumber, sections, pageRanges);
            
            if (targetSection >= 0 && targetSection < sections.Count)
            {
                distribution[targetSection][imageId] = imageData;
            }
            else
            {
                // Default to first section
                distribution[0][imageId] = imageData;
            }
        }

        return distribution;
    }

    /// <summary>
    /// Find which section a page belongs to based on text positions
    /// </summary>
    private int FindSectionForPage(
        int pageNumber, 
        List<(string Text, int StartIndex, int EndIndex)> sections,
        List<(int PageNumber, int StartIndex, int EndIndex)> pageRanges)
    {
        if (pageRanges.Count == 0)
            return 0;

        // Find the text range for this page
        var pageRange = pageRanges.FirstOrDefault(p => p.PageNumber == pageNumber);
        if (pageRange.PageNumber == 0 && pageNumber > 0)
            return 0;

        int pageTextStart = pageRange.StartIndex;
        int pageTextEnd = pageRange.EndIndex;

        // Find which section overlaps with this page's text range
        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            bool overlaps = pageTextStart < section.EndIndex && pageTextEnd > section.StartIndex;
            
            if (overlaps)
                return i;
        }

        // If no overlap found, find the closest section
        for (int i = 0; i < sections.Count; i++)
        {
            if (sections[i].StartIndex <= pageTextStart && 
                (i == sections.Count - 1 || sections[i + 1].StartIndex > pageTextStart))
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// Convert plain text to HTML with embedded images
    /// </summary>
    private string ConvertTextToHtmlWithImages(string text, Dictionary<string, byte[]> images)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "<p>No content</p>";

        var sb = new StringBuilder();
        
        // Escape HTML special characters
        var html = System.Net.WebUtility.HtmlEncode(text);
        
        // Convert line breaks to HTML
        html = html.Replace("\r\n", "\n").Replace("\r", "\n");
        
        // Split into paragraphs (double newlines)
        var paragraphs = Regex.Split(html, @"\n\s*\n");
        
        foreach (var para in paragraphs)
        {
            if (!string.IsNullOrWhiteSpace(para))
            {
                // Replace single newlines with <br/>
                var content = para.Trim().Replace("\n", "<br/>");
                sb.AppendLine($"<p>{content}</p>");
            }
        }

        // Add images at the end of the content
        if (images.Count > 0)
        {
            sb.AppendLine("<div class=\"embedded-images\" style=\"margin-top: 20px;\">");
            foreach (var image in images)
            {
                var base64Data = Convert.ToBase64String(image.Value);
                var mimeType = GetMimeType(image.Value);
                sb.AppendLine($"<p><img src=\"data:{mimeType};base64,{base64Data}\" alt=\"Embedded Image\" style=\"max-width:100%;\"/></p>");
            }
            sb.AppendLine("</div>");
        }

        return sb.Length > 0 ? sb.ToString() : "<p>No content</p>";
    }

    /// <summary>
    /// Get MIME type from image bytes
    /// </summary>
    private string GetMimeType(byte[] imageData)
    {
        if (imageData.Length < 4)
            return "image/png";

        // PNG
        if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            return "image/png";

        // JPEG
        if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            return "image/jpeg";

        // GIF
        if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x38)
            return "image/gif";

        // BMP
        if (imageData[0] == 0x42 && imageData[1] == 0x4D)
            return "image/bmp";

        // WebP
        if (imageData.Length >= 12 &&
            imageData[0] == 0x52 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x46 &&
            imageData[8] == 0x57 && imageData[9] == 0x45 && imageData[10] == 0x42 && imageData[11] == 0x50)
            return "image/webp";

        return "image/png";
    }

    /// <summary>
    /// Extract email metadata from a text section
    /// </summary>
    private (string? From, string? To, string? Cc, DateTime? Date, string? Subject) ExtractEmailMetadata(string text)
    {
        string? from = null;
        string? to = null;
        string? cc = null;
        DateTime? date = null;
        string? subject = null;

        // Build regex patterns
        var fromPattern = $@"(?:{string.Join("|", FromPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var toPattern = $@"(?:{string.Join("|", ToPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var ccPattern = $@"(?:{string.Join("|", CcPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var sentPattern = $@"(?:{string.Join("|", SentPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var subjectPattern = $@"(?:{string.Join("|", SubjectPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";

        var fromMatch = Regex.Match(text, fromPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (fromMatch.Success)
            from = fromMatch.Groups[1].Value.Trim();

        var toMatch = Regex.Match(text, toPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (toMatch.Success)
            to = toMatch.Groups[1].Value.Trim();

        var ccMatch = Regex.Match(text, ccPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (ccMatch.Success)
            cc = ccMatch.Groups[1].Value.Trim();

        var dateMatch = Regex.Match(text, sentPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (dateMatch.Success)
        {
            var dateStr = dateMatch.Groups[1].Value.Trim();
            if (DateTime.TryParse(dateStr, out var parsedDate))
                date = parsedDate;
            else
            {
                var cleanedDateStr = dateStr.Replace(" at ", " ").Replace(" à ", " ").Replace(" um ", " ");
                if (DateTime.TryParse(cleanedDateStr, out parsedDate))
                    date = parsedDate;
            }
        }

        var subjectMatch = Regex.Match(text, subjectPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (subjectMatch.Success)
            subject = subjectMatch.Groups[1].Value.Trim();

        return (from, to, cc, date, subject);
    }

    /// <summary>
    /// Create a single correspondence from the entire email
    /// </summary>
    private Correspondence CreateSingleCorrespondence(EmailMessage email)
    {
        string htmlContent;
        if (!string.IsNullOrWhiteSpace(email.HtmlBody))
        {
            htmlContent = email.EmbeddedImages.Count > 0 
                ? AddImagesToHtml(email.HtmlBody, email.EmbeddedImages)
                : email.HtmlBody;
        }
        else
        {
            htmlContent = ConvertTextToHtmlWithImages(email.TextBody, email.EmbeddedImages);
        }
        
        return new Correspondence
        {
            From = email.From,
            To = email.To,
            Cc = email.Cc,
            SentOn = email.SentOn,
            Subject = email.Subject,
            HtmlContent = htmlContent,
            TextContent = email.TextBody,
            Index = 0,
            IsParent = true,
            EmbeddedImages = new Dictionary<string, byte[]>(email.EmbeddedImages),
            Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
        };
    }
}
