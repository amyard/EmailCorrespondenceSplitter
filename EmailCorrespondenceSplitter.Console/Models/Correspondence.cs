namespace EmailCorrespondenceSplitter.Models;

/// <summary>
/// Represents a single correspondence extracted from an email thread
/// </summary>
public class Correspondence
{
    /// <summary>
    /// From address of this correspondence
    /// </summary>
    public string From { get; set; } = string.Empty;
    
    /// <summary>
    /// To address of this correspondence
    /// </summary>
    public string To { get; set; } = string.Empty;
    
    /// <summary>
    /// Date and time this correspondence was sent
    /// </summary>
    public DateTime? SentOn { get; set; }
    
    /// <summary>
    /// Subject of this correspondence
    /// </summary>
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// HTML content of this correspondence
    /// </summary>
    public string HtmlContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Plain text content of this correspondence
    /// </summary>
    public string TextContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Index in the email thread (0 = most recent, higher = older)
    /// </summary>
    public int Index { get; set; }
    
    /// <summary>
    /// Whether this is the original (parent) email
    /// </summary>
    public bool IsParent { get; set; }
    
    /// <summary>
    /// Embedded images referenced in the HTML content
    /// Key: Content-ID (cid:xxx), Value: Image data as byte array
    /// </summary>
    public Dictionary<string, byte[]> EmbeddedImages { get; set; } = new();
    
    /// <summary>
    /// Regular attachments (non-embedded files)
    /// Key: Filename, Value: File data as byte array
    /// </summary>
    public Dictionary<string, byte[]> Attachments { get; set; } = new();
}
