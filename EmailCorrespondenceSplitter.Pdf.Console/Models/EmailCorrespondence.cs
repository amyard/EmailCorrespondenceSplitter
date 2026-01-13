namespace EmailCorrespondenceSplitter.Pdf.Console.Models;

/// <summary>
/// Represents a single correspondence extracted from an email
/// </summary>
public class EmailCorrespondence
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public DateTime? SentDate { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Index { get; set; }
}
