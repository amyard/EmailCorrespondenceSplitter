using System.Text;
using System.Text.RegularExpressions;
using EmailCorrespondenceSplitter.Pdf.Console.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Parser for PDF email files
/// </summary>
public class PdfEmailParser : IEmailParser
{
    public async Task<EmailMessage> ParseAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var textContent = ExtractTextFromPdf(filePath);
            
            // Try to extract email metadata from the text
            var metadata = ExtractEmailMetadata(textContent);
            
            var emailMessage = new EmailMessage
            {
                Subject = metadata.Subject ?? Path.GetFileNameWithoutExtension(filePath),
                From = metadata.From ?? string.Empty,
                To = metadata.To ?? string.Empty,
                Cc = metadata.Cc ?? string.Empty,
                SentOn = metadata.Date,
                HtmlBody = ConvertTextToHtml(textContent),
                TextBody = textContent,
                FilePath = filePath,
                EmailType = EmailType.Generic
            };

            return emailMessage;
        });
    }

    public bool CanParse(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extract all text content from a PDF file
    /// </summary>
    private string ExtractTextFromPdf(string filePath)
    {
        var sb = new StringBuilder();

        using var pdfReader = new PdfReader(filePath);
        using var pdfDocument = new PdfDocument(pdfReader);

        for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
        {
            var page = pdfDocument.GetPage(i);
            var strategy = new SimpleTextExtractionStrategy();
            var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
            
            sb.AppendLine(pageText);
            
            // Add page separator if not the last page
            if (i < pdfDocument.GetNumberOfPages())
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Convert plain text to HTML, preserving line breaks
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

        return sb.ToString();
    }

    /// <summary>
    /// Extract email metadata from text content
    /// </summary>
    private (string? From, string? To, string? Cc, DateTime? Date, string? Subject) ExtractEmailMetadata(string text)
    {
        string? from = null;
        string? to = null;
        string? cc = null;
        DateTime? date = null;
        string? subject = null;

        // Multi-language From patterns
        var fromPatterns = new[] { "From", "Von", "De", "Da", "??", "Od", "Från", "Fra" };
        var toPatterns = new[] { "To", "An", "À", "A", "????", "Do", "Till", "Til" };
        var ccPatterns = new[] { "Cc", "CC", "Kopie", "Copie", "Copia", "?????", "Kopia" };
        var sentPatterns = new[] { "Sent", "Date", "Gesendet", "Envoyé", "Enviado", "Inviato", "??????????", "Datum" };
        var subjectPatterns = new[] { "Subject", "Betreff", "Objet", "Asunto", "Oggetto", "????", "Temat", "Ämne", "Emne" };

        // Build regex patterns
        var fromPattern = $@"(?:{string.Join("|", fromPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var toPattern = $@"(?:{string.Join("|", toPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var ccPattern = $@"(?:{string.Join("|", ccPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var sentPattern = $@"(?:{string.Join("|", sentPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var subjectPattern = $@"(?:{string.Join("|", subjectPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";

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
            if (DateTime.TryParse(dateStr, out var parsedDate))
            {
                date = parsedDate;
            }
        }

        var subjectMatch = Regex.Match(text, subjectPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (subjectMatch.Success)
        {
            subject = subjectMatch.Groups[1].Value.Trim();
        }

        return (from, to, cc, date, subject);
    }
}
