using EmailCorrespondenceSplitter.Pdf.Console.Models;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Orchestrates the email splitting process with PDF output
/// </summary>
public class PdfEmailSplitter
{
    private readonly MsgEmailParser _parser;
    private readonly CorrespondenceDetector _detector;
    private readonly PdfOutputManager _outputManager;

    public PdfEmailSplitter(string outputDirectory)
    {
        _parser = new MsgEmailParser();
        _detector = new CorrespondenceDetector();
        _outputManager = new PdfOutputManager(outputDirectory);
    }

    /// <summary>
    /// Process all MSG files in the specified directory
    /// </summary>
    public async Task ProcessDirectoryAsync(string inputDirectory)
    {
        if (!Directory.Exists(inputDirectory))
        {
            System.Console.WriteLine($"Input directory not found: {inputDirectory}");
            return;
        }

        var msgFiles = Directory.GetFiles(inputDirectory, "*.msg", SearchOption.TopDirectoryOnly);

        if (msgFiles.Length == 0)
        {
            System.Console.WriteLine("No MSG files found in the Assets directory.");
            return;
        }

        System.Console.WriteLine($"Found {msgFiles.Length} MSG file(s) to process.\n");

        foreach (var msgFile in msgFiles)
        {
            await ProcessEmailAsync(msgFile);
        }

        System.Console.WriteLine("\nProcessing complete!");
    }

    /// <summary>
    /// Process a single MSG file and return the number of correspondences found
    /// </summary>
    public async Task<int> ProcessEmailAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        System.Console.WriteLine($"Processing: {fileName}");

        try
        {
            // Parse the email
            var email = await _parser.ParseAsync(filePath);
            System.Console.WriteLine($"  Email type detected: {email.EmailType}");
            System.Console.WriteLine($"  Subject: {email.Subject}");
            System.Console.WriteLine($"  From: {email.From}");

            // Detect correspondences
            var correspondences = _detector.DetectCorrespondences(email);
            System.Console.WriteLine($"  Found {correspondences.Count} correspondence(s)");

            if (correspondences.Count == 0)
            {
                System.Console.WriteLine("  No correspondences detected, skipping.");
                return 0;
            }

            // Create output folder for this email
            var outputFolder = _outputManager.CreateEmailFolder(filePath);
            System.Console.WriteLine($"  Output folder: {outputFolder}");

            // Copy the original parent email
            _outputManager.CopyParentEmail(filePath, outputFolder);
            System.Console.WriteLine("  Copied parent email");

            // Save each correspondence as PDF
            foreach (var correspondence in correspondences)
            {
                await _outputManager.SaveCorrespondenceAsync(correspondence, outputFolder);
                System.Console.WriteLine($"  Saved correspondence {correspondence.Index + 1}: {correspondence.From}");
            }

            System.Console.WriteLine($"  Successfully processed {fileName}\n");
            return correspondences.Count;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"  Error processing {fileName}: {ex.Message}\n");
            return 0;
        }
    }
}
