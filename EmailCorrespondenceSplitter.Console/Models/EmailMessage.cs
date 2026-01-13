namespace EmailCorrespondenceSplitter.Models;

/// <summary>
/// Represents an email message with all its components
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Subject of the email
    /// </summary>
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// Email sender
    /// </summary>
    public string From { get; set; } = string.Empty;
    
    /// <summary>
    /// Email recipients
    /// </summary>
    public string To { get; set; } = string.Empty;
    
    /// <summary>
    /// CC recipients
    /// </summary>
    public string Cc { get; set; } = string.Empty;
    
    /// <summary>
    /// Date and time the email was sent
    /// </summary>
    public DateTime? SentOn { get; set; }
    
    /// <summary>
    /// HTML body of the email
    /// </summary>
    public string HtmlBody { get; set; } = string.Empty;
    
    /// <summary>
    /// Plain text body of the email
    /// </summary>
    public string TextBody { get; set; } = string.Empty;
    
    /// <summary>
    /// Email client type (Outlook, Gmail, Apple, etc.)
    /// </summary>
    public EmailType EmailType { get; set; }
    
    /// <summary>
    /// Original file path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// List of attachment filenames
    /// </summary>
    public List<string> Attachments { get; set; } = new();
    
    /// <summary>
    /// Attachment data
    /// Key: Filename, Value: File data as byte array
    /// </summary>
    public Dictionary<string, byte[]> AttachmentData { get; set; } = new();
    
    /// <summary>
    /// Embedded images from the email
    /// Key: Content-ID (without cid: prefix), Value: Image data as byte array
    /// </summary>
    public Dictionary<string, byte[]> EmbeddedImages { get; set; } = new();
}

/// <summary>
/// Email client types with expanded support for common email clients
/// </summary>
public enum EmailType
{
    Unknown,
    Outlook,
    Gmail,
    Apple,
    Thunderbird,
    YahooMail,
    Office365,
    OutlookWeb,
    ProtonMail,
    ZohaMail,
    Generic,  // For standard RFC-compliant emails
    Other
}
