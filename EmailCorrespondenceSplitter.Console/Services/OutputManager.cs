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
            
            // Process HTML content with embedded images
            string htmlContent = correspondence.HtmlContent;
            
            if (!string.IsNullOrWhiteSpace(htmlContent) && correspondence.EmbeddedImages.Count > 0)
            {
                // Convert cid: references to base64 data URLs for HTML rendering
                htmlContent = EmbedImagesAsBase64(htmlContent, correspondence.EmbeddedImages);
                
                // Also add images as attachments to the MSG file
                // Note: MsgKit has limited support for inline images, so the base64 embedding
                // in HTML is the primary way images will render when the MSG file is opened
                foreach (var imageEntry in correspondence.EmbeddedImages)
                {
                    var contentId = imageEntry.Key;
                    var imageData = imageEntry.Value;
                    
                    // Determine file extension from image data (basic detection)
                    var extension = GetImageExtension(imageData);
                    var imageName = $"image_{contentId.Replace("@", "_").Replace(".", "_")}{extension}";
                    
                    try
                    {
                        // Save image data to a temporary file
                        var tempImagePath = Path.Combine(Path.GetTempPath(), imageName);
                        File.WriteAllBytes(tempImagePath, imageData);
                        
                        // Add as attachment using the file path
                        email.Attachments.Add(tempImagePath);
                        
                        // Note: Temp files will be cleaned up by the system eventually
                        // For production, consider implementing proper cleanup
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Warning: Could not add image {imageName} to MSG: {ex.Message}");
                    }
                }
            }
            
            // Set body (prefer HTML, fallback to text)
            if (!string.IsNullOrWhiteSpace(htmlContent))
            {
                email.BodyHtml = htmlContent;
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
    /// Convert cid: image references to base64 data URLs in HTML
    /// </summary>
    private string EmbedImagesAsBase64(string htmlContent, Dictionary<string, byte[]> embeddedImages)
    {
        // Pattern to match cid: references: src="cid:xxx" or src='cid:xxx'
        var cidPattern = @"((?:src|background)\s*=\s*['""])cid:([^'""]+)(['""])";
        
        var result = Regex.Replace(htmlContent, cidPattern, (match) =>
        {
            var prefix = match.Groups[1].Value; // src=" or src='
            var contentId = match.Groups[2].Value;
            var suffix = match.Groups[3].Value; // " or '
            
            // Try to find the image data
            if (embeddedImages.TryGetValue(contentId, out var imageData))
            {
                // Convert to base64 data URL
                var base64 = Convert.ToBase64String(imageData);
                var mimeType = GetImageMimeType(imageData);
                var dataUrl = $"data:{mimeType};base64,{base64}";
                
                return $"{prefix}{dataUrl}{suffix}";
            }
            
            // If image not found, keep original cid: reference
            Console.WriteLine($"  Warning: Could not embed image cid:{contentId} - data not found");
            return match.Value;
        }, RegexOptions.IgnoreCase);
        
        return result;
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
