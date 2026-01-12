using System.Text;
using EmailCorrespondenceSplitter.Models;

namespace EmailCorrespondenceSplitter.Services;

/// <summary>
/// Manages output folders and file operations for saving correspondences
/// </summary>
public class OutputManager
{
    private readonly string _outputBaseFolder;
    
    public OutputManager(string outputBaseFolder = "Output")
    {
        _outputBaseFolder = outputBaseFolder;
        
        // Create base output folder if it doesn't exist
        if (!Directory.Exists(_outputBaseFolder))
        {
            Directory.CreateDirectory(_outputBaseFolder);
        }
    }
    
    /// <summary>
    /// Create a folder for an email based on its filename
    /// </summary>
    /// <param name="emailFilePath">Path to the email file</param>
    /// <returns>Path to the created folder</returns>
    public string CreateEmailFolder(string emailFilePath)
    {
        var emailFileName = Path.GetFileNameWithoutExtension(emailFilePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var folderName = $"{emailFileName}_{timestamp}";
        
        // Sanitize folder name
        folderName = SanitizeFileName(folderName);
        
        var folderPath = Path.Combine(_outputBaseFolder, folderName);
        Directory.CreateDirectory(folderPath);
        
        return folderPath;
    }
    
    /// <summary>
    /// Save a correspondence as an HTML file
    /// </summary>
    public async Task SaveCorrespondenceAsync(Correspondence correspondence, string outputFolder, EmailType emailType)
    {
        var fileName = $"{(correspondence.Index + 1):D2}_correspondence_{SanitizeFileName(correspondence.From)}.html";
        var filePath = Path.Combine(outputFolder, fileName);
        
        var html = GenerateEmailHtml(
            correspondence.Subject,
            correspondence.From,
            correspondence.To,
            string.Empty,
            correspondence.SentOn,
            correspondence.HtmlContent,
            $"Correspondence Index: {correspondence.Index + 1} | Is Parent: {correspondence.IsParent} | Email Type: {emailType}"
        );
        
        await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);
    }
    
    /// <summary>
    /// Generate a complete HTML document for an email or correspondence
    /// </summary>
    private string GenerateEmailHtml(string subject, string from, string to, string cc, DateTime? sentOn, string htmlBody, string metadata)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"utf-8\">");
        sb.AppendLine($"    <title>{EscapeHtml(subject)}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("        .email-header { background-color: #f0f0f0; padding: 15px; border-radius: 5px; margin-bottom: 20px; }");
        sb.AppendLine("        .email-header-field { margin: 5px 0; }");
        sb.AppendLine("        .email-header-label { font-weight: bold; display: inline-block; width: 100px; }");
        sb.AppendLine("        .email-metadata { color: #666; font-size: 0.9em; margin-bottom: 10px; }");
        sb.AppendLine("        .email-body { border-top: 1px solid #ccc; padding-top: 20px; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        
        // Email header
        sb.AppendLine("    <div class=\"email-header\">");
        sb.AppendLine($"        <div class=\"email-header-field\"><span class=\"email-header-label\">Subject:</span> {EscapeHtml(subject)}</div>");
        sb.AppendLine($"        <div class=\"email-header-field\"><span class=\"email-header-label\">From:</span> {EscapeHtml(from)}</div>");
        sb.AppendLine($"        <div class=\"email-header-field\"><span class=\"email-header-label\">To:</span> {EscapeHtml(to)}</div>");
        
        if (!string.IsNullOrWhiteSpace(cc))
        {
            sb.AppendLine($"        <div class=\"email-header-field\"><span class=\"email-header-label\">Cc:</span> {EscapeHtml(cc)}</div>");
        }
        
        if (sentOn.HasValue)
        {
            sb.AppendLine($"        <div class=\"email-header-field\"><span class=\"email-header-label\">Sent:</span> {sentOn.Value:yyyy-MM-dd HH:mm:ss}</div>");
        }
        
        sb.AppendLine("    </div>");
        
        // Metadata
        sb.AppendLine($"    <div class=\"email-metadata\">{EscapeHtml(metadata)}</div>");
        
        // Email body
        sb.AppendLine("    <div class=\"email-body\">");
        sb.AppendLine(htmlBody);
        sb.AppendLine("    </div>");
        
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Sanitize a string to be used as a filename
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        
        // Limit length to 50 characters
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }
        
        return sanitized;
    }
    
    /// <summary>
    /// Escape HTML special characters
    /// </summary>
    private string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
            
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
