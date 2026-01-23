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

        // Split HTML content if available - try to preserve styled content
        var htmlSections = SplitHtmlContent(htmlContent, textSections.Count);
        
        // If HTML splitting failed, try to extract HTML sections based on text positions
        bool htmlSplitSucceeded = htmlSections.Any(h => !string.IsNullOrWhiteSpace(h));
        if (!htmlSplitSucceeded && !string.IsNullOrWhiteSpace(htmlContent))
        {
            htmlSections = ExtractHtmlSectionsByTextPosition(htmlContent, textContent, textSections);
        }

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
            
            // Build HTML content - use styled HTML section if available, otherwise convert text with styling
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
                // Fallback to converting text to styled HTML with images
                sectionHtml = ConvertTextToStyledHtml(sectionText, correspondenceImages, htmlContent);
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
    /// Extract HTML sections by matching text positions to HTML content
    /// This is used when direct HTML splitting fails
    /// </summary>
    private List<string> ExtractHtmlSectionsByTextPosition(
        string htmlContent, 
        string textContent,
        List<(string Text, int StartIndex, int EndIndex)> textSections)
    {
        var htmlSections = new List<string>();
        
        // Build a mapping between plain text and HTML
        // Extract plain text from HTML for comparison
        var plainFromHtml = Regex.Replace(htmlContent, @"<[^>]+>", "");
        plainFromHtml = System.Net.WebUtility.HtmlDecode(plainFromHtml);
        
        // For each text section, find the corresponding first line in the HTML
        var fromPatternString = string.Join("|", FromPatterns.Select(Regex.Escape));
        
        foreach (var section in textSections)
        {
            // Find the "From:" line in this section
            var fromMatch = Regex.Match(section.Text, $@"^\s*({fromPatternString}):\s*(.+?)$", 
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            
            if (fromMatch.Success)
            {
                var searchText = fromMatch.Groups[1].Value + ":";
                var fromValue = fromMatch.Groups[2].Value.Trim();
                
                // Find this in the HTML - look for the pattern with possible tags
                var htmlSearchPattern = $@"(?:<[^>]+>)*\s*{Regex.Escape(searchText)}\s*(?:<[^>]+>)*\s*{Regex.Escape(fromValue.Substring(0, Math.Min(20, fromValue.Length)))}";
                
                var htmlMatch = Regex.Match(htmlContent, htmlSearchPattern, RegexOptions.IgnoreCase);
                if (htmlMatch.Success)
                {
                    // Found it - we'll use this position for splitting
                    // But for now, just add an empty string since we can't easily extract the exact section
                    htmlSections.Add(string.Empty);
                }
                else
                {
                    htmlSections.Add(string.Empty);
                }
            }
            else
            {
                htmlSections.Add(string.Empty);
            }
        }
        
        return htmlSections;
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
        
        // Multiple patterns to find "From:" in various HTML structures from PDF extraction
        // The PDF parser generates HTML with <p>, <h1-h6>, <strong>, <em>, <span>, <li> tags
        var htmlFromPatterns = new[]
        {
            // Pattern 1: <p>From: or <p><strong>From: or <p><span>From: etc.
            $@"<p[^>]*>(?:\s*<[^>]+>)*\s*(?:{fromPatternString}):\s*",
            // Pattern 2: Header tags with From:
            $@"<h[1-6][^>]*>(?:\s*<[^>]+>)*\s*(?:{fromPatternString}):\s*",
            // Pattern 3: Just the From: with optional styling tags (for simpler structures)
            $@"(?:<strong>|<b>|<span[^>]*>)*\s*(?:{fromPatternString}):\s*",
            // Pattern 4: List item with From:
            $@"<li[^>]*>(?:\s*<[^>]+>)*\s*(?:{fromPatternString}):\s*",
            // Pattern 5: Any tag followed by From: pattern
            $@"(?:<[^>]+>\s*)*(?:{fromPatternString}):\s*[^<]+",
        };
        
        MatchCollection? bestMatches = null;
        
        foreach (var pattern in htmlFromPatterns)
        {
            var matches = Regex.Matches(htmlContent, pattern, RegexOptions.IgnoreCase);
            if (matches.Count >= expectedSections)
            {
                bestMatches = matches;
                break;
            }
            // Keep the best partial match
            if (bestMatches == null || matches.Count > bestMatches.Count)
            {
                bestMatches = matches;
            }
        }
        
        if (bestMatches != null && bestMatches.Count >= expectedSections)
        {
            // We have enough matches to split - each section starts at a "From:" match
            var splitPoints = new List<int>();
            
            // Take only the first expectedSections matches (one per section)
            for (int i = 0; i < expectedSections && i < bestMatches.Count; i++)
            {
                splitPoints.Add(bestMatches[i].Index);
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
        return ConvertTextToStyledHtml(text, images, null);
    }

    /// <summary>
    /// Convert plain text to styled HTML with embedded images
    /// Preserves styling from the original HTML where it exists
    /// </summary>
    private string ConvertTextToStyledHtml(string text, Dictionary<string, byte[]> images, string? originalHtml)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "<p>No content</p>";

        // If we have original HTML, try to extract styled segments
        var styledSegments = new Dictionary<string, StyledSegment>();
        if (!string.IsNullOrWhiteSpace(originalHtml))
        {
            styledSegments = ExtractStyledSegments(originalHtml);
        }

        var sb = new StringBuilder();
        
        // Convert line breaks to consistent format
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        
        // Split into lines for processing
        var lines = text.Split('\n');
        
        bool inParagraph = false;
        bool lastLineWasEmpty = false;
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                if (inParagraph)
                {
                    sb.AppendLine("</p>");
                    inParagraph = false;
                }
                lastLineWasEmpty = true;
                continue;
            }
            
            // Check if this line has styling in the original HTML
            var styledLine = ApplyOriginalStyling(trimmedLine, styledSegments);
            
            if (lastLineWasEmpty || !inParagraph)
            {
                if (inParagraph)
                {
                    sb.AppendLine("</p>");
                }
                sb.Append("<p>");
                inParagraph = true;
            }
            else
            {
                sb.Append("<br/>");
            }
            
            sb.Append(styledLine);
            lastLineWasEmpty = false;
        }
        
        if (inParagraph)
        {
            sb.AppendLine("</p>");
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
    /// Extract styled segments from original HTML
    /// </summary>
    private Dictionary<string, StyledSegment> ExtractStyledSegments(string html)
    {
        var segments = new Dictionary<string, StyledSegment>(StringComparer.OrdinalIgnoreCase);
        
        // Extract text with <strong> or <b> tags
        var boldPattern = @"<(?:strong|b)(?:\s[^>]*)?>([^<]+)</(?:strong|b)>";
        foreach (Match match in Regex.Matches(html, boldPattern, RegexOptions.IgnoreCase))
        {
            var text = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            if (!string.IsNullOrWhiteSpace(text) && !segments.ContainsKey(text))
            {
                segments[text] = new StyledSegment { Text = text, IsBold = true };
            }
        }
        
        // Extract text with <em> or <i> tags
        var italicPattern = @"<(?:em|i)(?:\s[^>]*)?>([^<]+)</(?:em|i)>";
        foreach (Match match in Regex.Matches(html, italicPattern, RegexOptions.IgnoreCase))
        {
            var text = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (segments.TryGetValue(text, out var existing))
                {
                    existing.IsItalic = true;
                }
                else
                {
                    segments[text] = new StyledSegment { Text = text, IsItalic = true };
                }
            }
        }
        
        // Extract text with inline styles (span with style attribute)
        var stylePattern = @"<span\s+style=""([^""]+)"">([^<]+)</span>";
        foreach (Match match in Regex.Matches(html, stylePattern, RegexOptions.IgnoreCase))
        {
            var style = match.Groups[1].Value;
            var text = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim());
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (segments.TryGetValue(text, out var existing))
                {
                    existing.InlineStyle = style;
                }
                else
                {
                    segments[text] = new StyledSegment { Text = text, InlineStyle = style };
                }
            }
        }
        
        // Extract header content (h1-h6)
        var headerPattern = @"<(h[1-6])(?:\s[^>]*)?>([^<]+)</\1>";
        foreach (Match match in Regex.Matches(html, headerPattern, RegexOptions.IgnoreCase))
        {
            var headerTag = match.Groups[1].Value.ToLower();
            var text = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim());
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (segments.TryGetValue(text, out var existing))
                {
                    existing.HeaderLevel = headerTag;
                }
                else
                {
                    segments[text] = new StyledSegment { Text = text, HeaderLevel = headerTag };
                }
            }
        }
        
        // Extract list item content
        var listItemPattern = @"<li(?:\s[^>]*)?>([^<]+)</li>";
        foreach (Match match in Regex.Matches(html, listItemPattern, RegexOptions.IgnoreCase))
        {
            var text = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (segments.TryGetValue(text, out var existing))
                {
                    existing.IsListItem = true;
                }
                else
                {
                    segments[text] = new StyledSegment { Text = text, IsListItem = true };
                }
            }
        }
        
        return segments;
    }

    /// <summary>
    /// Apply original styling to a line of text
    /// </summary>
    private string ApplyOriginalStyling(string line, Dictionary<string, StyledSegment> styledSegments)
    {
        // First, escape the line for HTML
        var escapedLine = System.Net.WebUtility.HtmlEncode(line);
        
        if (styledSegments.Count == 0)
        {
            return escapedLine;
        }
        
        // Check if the entire line matches a styled segment
        if (styledSegments.TryGetValue(line, out var lineStyle))
        {
            return ApplyStyle(escapedLine, lineStyle);
        }
        
        // Check for partial matches within the line
        var result = escapedLine;
        foreach (var segment in styledSegments)
        {
            if (line.Contains(segment.Key, StringComparison.OrdinalIgnoreCase))
            {
                var escapedSegment = System.Net.WebUtility.HtmlEncode(segment.Key);
                var styledSegment = ApplyStyle(escapedSegment, segment.Value);
                
                // Replace in result (case-insensitive)
                var pattern = Regex.Escape(escapedSegment);
                result = Regex.Replace(result, pattern, styledSegment, RegexOptions.IgnoreCase);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Apply style to text based on StyledSegment properties
    /// </summary>
    private string ApplyStyle(string text, StyledSegment style)
    {
        var result = text;
        
        // Apply bold
        if (style.IsBold)
        {
            result = $"<strong>{result}</strong>";
        }
        
        // Apply italic
        if (style.IsItalic)
        {
            result = $"<em>{result}</em>";
        }
        
        // Apply inline style
        if (!string.IsNullOrEmpty(style.InlineStyle))
        {
            result = $"<span style=\"{style.InlineStyle}\">{result}</span>";
        }
        
        return result;
    }

    /// <summary>
    /// Represents a styled segment of text from the original HTML
    /// </summary>
    private class StyledSegment
    {
        public string Text { get; set; } = "";
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public string? InlineStyle { get; set; }
        public string? HeaderLevel { get; set; }
        public bool IsListItem { get; set; }
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
