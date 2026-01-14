namespace EmailCorrespondenceSplitter.Pdf.Console.Models;

/// <summary>
/// Represents an email message with all its components
/// </summary>
public class EmailMessage
{
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Cc { get; set; } = string.Empty;
    public DateTime? SentOn { get; set; }
    public string HtmlBody { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public EmailType EmailType { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public List<string> Attachments { get; set; } = [];
    public Dictionary<string, byte[]> AttachmentData { get; set; } = [];
    public Dictionary<string, byte[]> EmbeddedImages { get; set; } = [];
}
