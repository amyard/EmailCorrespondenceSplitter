namespace EmailCorrespondenceSplitter.Pdf.Console.Models;

/// <summary>
/// Represents a single correspondence extracted from an email thread
/// </summary>
public class Correspondence
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Cc { get; set; } = string.Empty;
    public DateTime? SentOn { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public int Index { get; set; }
    public bool IsParent { get; set; }
    public Dictionary<string, byte[]> EmbeddedImages { get; set; } = [];
    public Dictionary<string, byte[]> Attachments { get; set; } = [];
}
