using System.Text;
using System.Text.RegularExpressions;
using EmailCorrespondenceSplitter.Pdf.Console.Models;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Detects and extracts individual correspondences from PDF email content.
/// Splits by "From:" patterns in the text.
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

        if (string.IsNullOrWhiteSpace(textContent))
        {
            return [CreateSingleCorrespondence(email)];
        }

        // Build the split pattern for "From:" in multiple languages
        // Look for "From:" at the beginning of a line (with optional spaces)
        var fromPatternString = string.Join("|", FromPatterns.Select(Regex.Escape));
        var splitPattern = $@"(?=^\s*(?:{fromPatternString}):\s*.+$)";

        // Split the text content by "From:" lines
        var sections = Regex.Split(textContent, splitPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Filter out empty sections
        sections = sections.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

        if (sections.Length <= 1)
        {
            // No split found or only one section, return as single correspondence
            return [CreateSingleCorrespondence(email)];
        }

        for (int i = 0; i < sections.Length; i++)
        {
            var sectionText = sections[i].Trim();
            
            if (string.IsNullOrWhiteSpace(sectionText))
                continue;

            var metadata = ExtractEmailMetadata(sectionText);
            var htmlContent = ConvertTextToHtml(sectionText);

            correspondences.Add(new Correspondence
            {
                From = metadata.From ?? (i == 0 ? email.From : "Unknown"),
                To = metadata.To ?? (i == 0 ? email.To : string.Empty),
                Cc = metadata.Cc ?? string.Empty,
                SentOn = metadata.Date ?? (i == 0 ? email.SentOn : null),
                Subject = metadata.Subject ?? email.Subject,
                HtmlContent = htmlContent,
                TextContent = sectionText,
                Index = i,
                IsParent = i == 0,
                EmbeddedImages = i == 0 ? new Dictionary<string, byte[]>(email.EmbeddedImages) : [],
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
        {
            from = fromMatch.Groups[1].Value.Trim();
        }

        var toMatch = Regex.Match(text, toPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (toMatch.Success)
        {
            to = toMatch.Groups[1].Value.Trim();
        }

        var ccMatch = Regex.Match(text, ccPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (ccMatch.Success)
        {
            cc = ccMatch.Groups[1].Value.Trim();
        }

        var dateMatch = Regex.Match(text, sentPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (dateMatch.Success)
        {
            var dateStr = dateMatch.Groups[1].Value.Trim();
            // Try to parse various date formats
            if (DateTime.TryParse(dateStr, out var parsedDate))
            {
                date = parsedDate;
            }
            else
            {
                // Try cleaning up common date format issues
                var cleanedDateStr = dateStr.Replace(" at ", " ").Replace(" à ", " ").Replace(" um ", " ");
                if (DateTime.TryParse(cleanedDateStr, out parsedDate))
                {
                    date = parsedDate;
                }
            }
        }

        var subjectMatch = Regex.Match(text, subjectPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (subjectMatch.Success)
        {
            subject = subjectMatch.Groups[1].Value.Trim();
        }

        return (from, to, cc, date, subject);
    }

    /// <summary>
    /// Convert plain text to HTML, preserving line breaks and structure
    /// </summary>
    private string ConvertTextToHtml(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "<p>No content</p>";

        // Escape HTML special characters
        var html = System.Net.WebUtility.HtmlEncode(text);
        
        // Convert line breaks to HTML
        html = html.Replace("\r\n", "\n").Replace("\r", "\n");
        
        // Split into paragraphs (double newlines)
        var paragraphs = Regex.Split(html, @"\n\s*\n");
        
        var sb = new StringBuilder();
        foreach (var para in paragraphs)
        {
            if (!string.IsNullOrWhiteSpace(para))
            {
                // Replace single newlines with <br/>
                var content = para.Trim().Replace("\n", "<br/>");
                sb.AppendLine($"<p>{content}</p>");
            }
        }

        return sb.Length > 0 ? sb.ToString() : "<p>No content</p>";
    }

    /// <summary>
    /// Create a single correspondence from the entire email
    /// </summary>
    private Correspondence CreateSingleCorrespondence(EmailMessage email)
    {
        return new Correspondence
        {
            From = email.From,
            To = email.To,
            Cc = email.Cc,
            SentOn = email.SentOn,
            Subject = email.Subject,
            HtmlContent = email.HtmlBody,
            TextContent = email.TextBody,
            Index = 0,
            IsParent = true,
            EmbeddedImages = new Dictionary<string, byte[]>(email.EmbeddedImages),
            Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
        };
    }
}
