using System.Text;
using System.Text.RegularExpressions;
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
    /// Save a correspondence as a MSG file with embedded images
    /// </summary>
    public async System.Threading.Tasks.Task SaveCorrespondenceAsync(Correspondence correspondence, string outputFolder, EmailType emailType)
    {
        var fileName = $"{(correspondence.Index + 1):D2}_correspondence_{SanitizeFileName(correspondence.From)}.msg";
        var filePath = Path.Combine(outputFolder, fileName);
        
        await System.Threading.Tasks.Task.Run(() =>
        {
            using var email = new MsgKit.Email(
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
            
            // Set body - keep the original content exactly as is
            if (!string.IsNullOrWhiteSpace(correspondence.HtmlContent))
            {
                email.BodyHtml = correspondence.HtmlContent;
            }
            else if (!string.IsNullOrWhiteSpace(correspondence.TextContent))
            {
                email.BodyText = correspondence.TextContent;
            }
            
            // Add embedded images as inline attachments with proper Content-ID
            if (correspondence.EmbeddedImages.Count > 0)
            {
                foreach (var imageEntry in correspondence.EmbeddedImages)
                {
                    var contentId = imageEntry.Key;
                    var imageData = imageEntry.Value;
                    
                    // Determine file extension and MIME type from image data
                    var extension = GetImageExtension(imageData);
                    var mimeType = GetImageMimeType(imageData);
                    var imageName = $"image_{contentId.Replace("@", "_").Replace(".", "_")}{extension}";
                    
                    try
                    {
                        // Create a temporary file for the image
                        var tempImagePath = Path.Combine(Path.GetTempPath(), imageName);
                        File.WriteAllBytes(tempImagePath, imageData);
                        
                        // Add as inline attachment with Content-ID
                        // MsgKit should preserve the inline nature with the contentId parameter
                        email.Attachments.Add(tempImagePath, contentId: contentId);
                        
                        // Clean up the temporary file after adding to the email
                        try
                        {
                            File.Delete(tempImagePath);
                        }
                        catch
                        {
                            // If deletion fails, it's not critical - temp folder will be cleaned eventually
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Warning: Could not add embedded image cid:{contentId} to MSG: {ex.Message}");
                    }
                }
            }
            
            // Add regular attachments with their original filenames
            if (correspondence.Attachments.Count > 0)
            {
                foreach (var attachmentEntry in correspondence.Attachments)
                {
                    var originalFileName = attachmentEntry.Key;
                    var fileData = attachmentEntry.Value;
                    
                    try
                    {
                        // Create a temporary file with the original filename
                        var tempDir = Path.Combine(Path.GetTempPath(), $"EmailSplitter_{Guid.NewGuid():N}");
                        Directory.CreateDirectory(tempDir);
                        var tempAttachmentPath = Path.Combine(tempDir, originalFileName);
                        File.WriteAllBytes(tempAttachmentPath, fileData);
                        
                        // Add as regular attachment (no contentId)
                        // MsgKit will use the filename from the path
                        email.Attachments.Add(tempAttachmentPath);
                        
                        Console.WriteLine($"  Added attachment: {originalFileName} ({fileData.Length} bytes)");
                        
                        // Clean up the temporary file and directory after adding to the email
                        try
                        {
                            File.Delete(tempAttachmentPath);
                            Directory.Delete(tempDir, true);
                        }
                        catch
                        {
                            // If deletion fails, it's not critical - temp folder will be cleaned eventually
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Warning: Could not add attachment {originalFileName} to MSG: {ex.Message}");
                    }
                }
            }
            
            // Save the MSG file
            email.Save(filePath);
        });
    }
    
    /// <summary>
    /// Detect MIME type from image data
    /// </summary>
    private string GetImageMimeType(byte[] imageData)
    {
        if (imageData.Length < 4)
            return "image/png"; // Default
        
        // Check magic numbers
        // PNG: 89 50 4E 47
        if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            return "image/png";
        
        // JPEG: FF D8 FF
        if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            return "image/jpeg";
        
        // GIF: 47 49 46 38
        if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x38)
            return "image/gif";
        
        // BMP: 42 4D
        if (imageData[0] == 0x42 && imageData[1] == 0x4D)
            return "image/bmp";
        
        // WebP: 52 49 46 46 ... 57 45 42 50
        if (imageData.Length >= 12 && 
            imageData[0] == 0x52 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x46 &&
            imageData[8] == 0x57 && imageData[9] == 0x45 && imageData[10] == 0x42 && imageData[11] == 0x50)
            return "image/webp";
        
        return "image/png"; // Default fallback
    }
    
    /// <summary>
    /// Get file extension from image data
    /// </summary>
    private string GetImageExtension(byte[] imageData)
    {
        var mimeType = GetImageMimeType(imageData);
        return mimeType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/webp" => ".webp",
            _ => ".png"
        };
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
