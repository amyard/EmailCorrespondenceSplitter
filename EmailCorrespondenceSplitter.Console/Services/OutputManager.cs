using System.Text;
using System.Threading.Tasks;
using EmailCorrespondenceSplitter.Models;
using MsgKit;

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
    /// Save a correspondence as a MSG file
    /// </summary>
    public async System.Threading.Tasks.Task SaveCorrespondenceAsync(Correspondence correspondence, string outputFolder, EmailType emailType)
    {
        var fileName = $"{(correspondence.Index + 1):D2}_correspondence_{SanitizeFileName(correspondence.From)}.msg";
        var filePath = Path.Combine(outputFolder, fileName);
        
        await System.Threading.Tasks.Task.Run(() =>
        {
            using var email = new Email(
                new Sender(correspondence.From, correspondence.From),
                correspondence.Subject
            );
            
            // Add recipients
            if (!string.IsNullOrWhiteSpace(correspondence.To))
            {
                var recipients = correspondence.To.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var recipient in recipients)
                {
                    var cleanRecipient = recipient.Trim();
                    email.Recipients.AddTo(cleanRecipient, cleanRecipient);
                }
            }
            
            // Set sent date
            if (correspondence.SentOn.HasValue)
            {
                email.SentOn = correspondence.SentOn.Value;
            }
            
            // Set body (prefer HTML, fallback to text)
            if (!string.IsNullOrWhiteSpace(correspondence.HtmlContent))
            {
                email.BodyHtml = correspondence.HtmlContent;
            }
            else if (!string.IsNullOrWhiteSpace(correspondence.TextContent))
            {
                email.BodyText = correspondence.TextContent;
            }
            
            // Save the MSG file
            email.Save(filePath);
        });
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
}
