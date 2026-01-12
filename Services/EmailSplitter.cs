using EmailCorrespondenceSplitter.Models;

namespace EmailCorrespondenceSplitter.Services;

/// <summary>
/// Service to split emails into individual correspondences and save them
/// </summary>
public class EmailSplitter
{
    private readonly IEmailParser _emailParser;
    private readonly CorrespondenceDetector _correspondenceDetector;
    private readonly OutputManager _outputManager;
    
    public EmailSplitter(IEmailParser emailParser, CorrespondenceDetector correspondenceDetector, OutputManager outputManager)
    {
        _emailParser = emailParser;
        _correspondenceDetector = correspondenceDetector;
        _outputManager = outputManager;
    }
    
    /// <summary>
    /// Process an email file and split it into correspondences
    /// </summary>
    /// <param name="emailFilePath">Path to the email file</param>
    /// <returns>Number of correspondences extracted</returns>
    public async Task<int> ProcessEmailAsync(string emailFilePath)
    {
        if (!_emailParser.CanParse(emailFilePath))
        {
            Console.WriteLine($"Unsupported file type: {emailFilePath}");
            return 0;
        }
        
        Console.WriteLine($"Processing: {Path.GetFileName(emailFilePath)}");
        
        // Parse the email
        var email = await _emailParser.ParseAsync(emailFilePath);
        Console.WriteLine($"  Email Type: {email.EmailType}");
        Console.WriteLine($"  Subject: {email.Subject}");
        Console.WriteLine($"  From: {email.From}");
        Console.WriteLine($"  To: {email.To}");
        
        // Detect correspondences
        var correspondences = _correspondenceDetector.DetectCorrespondences(email);
        Console.WriteLine($"  Found {correspondences.Count} correspondence(s)");
        
        // Create output folder for this email
        var emailFolderPath = _outputManager.CreateEmailFolder(emailFilePath);
        Console.WriteLine($"  Output Folder: {emailFolderPath}");
        
        // Save the parent email
        await _outputManager.SaveParentEmailAsync(email, emailFolderPath);
        Console.WriteLine($"  Saved parent email");
        
        // Save each correspondence
        foreach (var correspondence in correspondences)
        {
            await _outputManager.SaveCorrespondenceAsync(correspondence, emailFolderPath, email.EmailType);
            Console.WriteLine($"  Saved correspondence {correspondence.Index}: {correspondence.From}");
        }
        
        Console.WriteLine($"  ? Completed processing {Path.GetFileName(emailFilePath)}\n");
        
        return correspondences.Count;
    }
    
    /// <summary>
    /// Process multiple email files
    /// </summary>
    /// <param name="emailFilePaths">List of email file paths</param>
    public async Task ProcessEmailsAsync(IEnumerable<string> emailFilePaths)
    {
        int totalEmails = 0;
        int totalCorrespondences = 0;
        int failedEmails = 0;
        
        foreach (var filePath in emailFilePaths)
        {
            try
            {
                var count = await ProcessEmailAsync(filePath);
                totalEmails++;
                totalCorrespondences += count;
            }
            catch (Exception ex)
            {
                failedEmails++;
                Console.WriteLine($"Error processing {Path.GetFileName(filePath)}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner error: {ex.InnerException.Message}");
                }
                Console.WriteLine();
            }
        }
        
        Console.WriteLine($"\n=== Summary ===");
        Console.WriteLine($"Total emails processed: {totalEmails}");
        Console.WriteLine($"Failed emails: {failedEmails}");
        Console.WriteLine($"Total correspondences extracted: {totalCorrespondences}");
    }
}
