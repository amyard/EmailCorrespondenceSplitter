using MsgReader.Outlook;
using HtmlAgilityPack;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Service to read and parse MSG (Outlook) email files
/// </summary>
public class EmailReader
{
    /// <summary>
    /// Read an email from a MSG file
    /// </summary>
    /// <param name="filePath">Path to the MSG file</param>
    /// <returns>Tuple containing (Subject, PlainTextBody)</returns>
    public (string Subject, string TextBody) ReadMsgFile(string filePath)
    {
        try
        {
            using var msg = new Storage.Message(filePath);
            
            var subject = msg.Subject ?? string.Empty;
            var textBody = string.Empty;

            // Try to get plain text body first
            try
            {
                textBody = msg.BodyText ?? string.Empty;
            }
            catch
            {
                // If plain text fails, try HTML and convert to text
                try
                {
                    var htmlBody = msg.BodyHtml ?? string.Empty;
                    if (!string.IsNullOrEmpty(htmlBody))
                    {
                        textBody = ConvertHtmlToText(htmlBody);
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Warning: Could not extract body from {Path.GetFileName(filePath)}: {ex.Message}");
                    textBody = "[Unable to extract email body]";
                }
            }

            return (subject, textBody);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error reading {Path.GetFileName(filePath)}: {ex.Message}");
            return (string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Convert HTML to plain text
    /// </summary>
    private string ConvertHtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Get inner text and clean up excessive whitespace
        var text = doc.DocumentNode.InnerText;
        
        // Decode HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);
        
        // Clean up excessive line breaks (more than 2 consecutive)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(\r?\n){3,}", "\n\n");
        
        return text.Trim();
    }
}
