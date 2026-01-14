using EmailCorrespondenceSplitter.Pdf.Console.Models;
using MsgKit;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Manages output folders and file operations for saving correspondences as MSG files
/// </summary>
public class OutputManager
{
    private readonly string _outputBaseFolder;

    public OutputManager(string outputBaseFolder)
    {
        _outputBaseFolder = outputBaseFolder;

        if (!Directory.Exists(_outputBaseFolder))
        {
            Directory.CreateDirectory(_outputBaseFolder);
        }
    }

    /// <summary>
    /// Create a folder for an email based on its filename
    /// </summary>
    public string CreateEmailFolder(string emailFilePath)
    {
        var emailFileName = Path.GetFileNameWithoutExtension(emailFilePath);
        var folderName = SanitizeFileName(emailFileName);

        var folderPath = Path.Combine(_outputBaseFolder, folderName);

        // If folder exists, append a number
        if (Directory.Exists(folderPath))
        {
            int counter = 1;
            while (Directory.Exists($"{folderPath}_{counter}"))
            {
                counter++;
            }
            folderPath = $"{folderPath}_{counter}";
        }

        Directory.CreateDirectory(folderPath);

        return folderPath;
    }

    /// <summary>
    /// Copy the original parent email to the output folder
    /// </summary>
    public void CopyParentEmail(string sourceFilePath, string outputFolder)
    {
        var fileName = $"00_parent_{Path.GetFileName(sourceFilePath)}";
        var destinationPath = Path.Combine(outputFolder, fileName);
        File.Copy(sourceFilePath, destinationPath, overwrite: true);
    }

    /// <summary>
    /// Save a correspondence as a MSG file
    /// </summary>
    public async System.Threading.Tasks.Task SaveCorrespondenceAsync(Correspondence correspondence, string outputFolder)
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

            // Add Cc recipients
            if (!string.IsNullOrWhiteSpace(correspondence.Cc))
            {
                var ccRecipients = correspondence.Cc.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var ccRecipient in ccRecipients)
                {
                    var cleanCcRecipient = ccRecipient.Trim();
                    email.Recipients.AddCc(cleanCcRecipient, cleanCcRecipient);
                }
            }

            // Set sent date
            if (correspondence.SentOn.HasValue)
            {
                email.SentOn = correspondence.SentOn.Value;
            }

            // Set body - keep original content as is
            if (!string.IsNullOrWhiteSpace(correspondence.HtmlContent))
            {
                email.BodyHtml = correspondence.HtmlContent;
            }
            else if (!string.IsNullOrWhiteSpace(correspondence.TextContent))
            {
                email.BodyText = correspondence.TextContent;
            }

            // Add embedded images
            if (correspondence.EmbeddedImages.Count > 0)
            {
                foreach (var imageEntry in correspondence.EmbeddedImages)
                {
                    var contentId = imageEntry.Key;
                    var imageData = imageEntry.Value;

                    var extension = GetImageExtension(imageData);
                    var imageName = $"image_{contentId.Replace("@", "_").Replace(".", "_")}{extension}";

                    try
                    {
                        var tempImagePath = Path.Combine(Path.GetTempPath(), imageName);
                        File.WriteAllBytes(tempImagePath, imageData);

                        email.Attachments.Add(tempImagePath, contentId: contentId);

                        try { File.Delete(tempImagePath); }
                        catch { /* Ignore cleanup errors */ }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"  Warning: Could not add embedded image cid:{contentId}: {ex.Message}");
                    }
                }
            }

            // Add regular attachments
            if (correspondence.Attachments.Count > 0)
            {
                foreach (var attachmentEntry in correspondence.Attachments)
                {
                    var originalFileName = attachmentEntry.Key;
                    var fileData = attachmentEntry.Value;

                    try
                    {
                        var tempDir = Path.Combine(Path.GetTempPath(), $"EmailSplitter_{Guid.NewGuid():N}");
                        Directory.CreateDirectory(tempDir);
                        var tempAttachmentPath = Path.Combine(tempDir, originalFileName);
                        File.WriteAllBytes(tempAttachmentPath, fileData);

                        email.Attachments.Add(tempAttachmentPath);

                        try
                        {
                            File.Delete(tempAttachmentPath);
                            Directory.Delete(tempDir, true);
                        }
                        catch { /* Ignore cleanup errors */ }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"  Warning: Could not add attachment {originalFileName}: {ex.Message}");
                    }
                }
            }

            email.Save(filePath);
        });
    }

    private string GetImageExtension(byte[] imageData)
    {
        if (imageData.Length < 4)
            return ".png";

        // PNG
        if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            return ".png";

        // JPEG
        if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            return ".jpg";

        // GIF
        if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x38)
            return ".gif";

        // BMP
        if (imageData[0] == 0x42 && imageData[1] == 0x4D)
            return ".bmp";

        // WebP
        if (imageData.Length >= 12 &&
            imageData[0] == 0x52 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x46 &&
            imageData[8] == 0x57 && imageData[9] == 0x45 && imageData[10] == 0x42 && imageData[11] == 0x50)
            return ".webp";

        return ".png";
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());

        // Limit length
        if (sanitized.Length > 50)
        {
            sanitized = sanitized[..50];
        }

        return sanitized;
    }
}
