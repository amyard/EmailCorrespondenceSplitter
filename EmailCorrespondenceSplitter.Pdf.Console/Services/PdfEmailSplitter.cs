using EmailCorrespondenceSplitter.Pdf.Console.Models;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Orchestrates the email splitting process with PDF output.
/// Supports both MSG and PDF input files.
/// </summary>
public class PdfEmailSplitter
{
    private readonly MsgEmailParser _msgParser;
    private readonly PdfEmailParser _pdfParser;
    private readonly CorrespondenceDetector _msgCorrespondenceDetector;
    private readonly PdfCorrespondenceDetector _pdfCorrespondenceDetector;
    private readonly PdfOutputManager _outputManager;
    private readonly PdfDirectCorrespondenceSplitter _pdfDirectSplitter;

    public PdfEmailSplitter(string outputDirectory)
    {
        _msgParser = new MsgEmailParser();
        _pdfParser = new PdfEmailParser();
        _msgCorrespondenceDetector = new CorrespondenceDetector();
        _pdfCorrespondenceDetector = new PdfCorrespondenceDetector();
        _outputManager = new PdfOutputManager(outputDirectory);
        _pdfDirectSplitter = new PdfDirectCorrespondenceSplitter();
    }

    /// <summary>
    /// Process all MSG and PDF files in the specified directory
    /// </summary>
    public async Task ProcessDirectoryAsync(string inputDirectory)
    {
        if (!Directory.Exists(inputDirectory))
        {
            System.Console.WriteLine($"Input directory not found: {inputDirectory}");
            return;
        }

        // Get both MSG and PDF files
        var msgFiles = Directory.GetFiles(inputDirectory, "*.msg", SearchOption.TopDirectoryOnly);
        var pdfFiles = Directory.GetFiles(inputDirectory, "*.pdf", SearchOption.TopDirectoryOnly);

        var totalFiles = msgFiles.Length + pdfFiles.Length;

        if (totalFiles == 0)
        {
            System.Console.WriteLine("No MSG or PDF files found in the Assets directory.");
            return;
        }

        System.Console.WriteLine($"Found {msgFiles.Length} MSG file(s) and {pdfFiles.Length} PDF file(s) to process.\n");

        // Process MSG files
        foreach (var msgFile in msgFiles)
        {
            await ProcessMsgEmailAsync(msgFile);
        }

        // Process PDF files
        foreach (var pdfFile in pdfFiles)
        {
            await ProcessPdfEmailAsync(pdfFile);
        }

        System.Console.WriteLine("\nProcessing complete!");
    }

    /// <summary>
    /// Process a single MSG file and return the number of correspondences found
    /// </summary>
    public async Task<int> ProcessMsgEmailAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        System.Console.WriteLine($"Processing MSG: {fileName}");

        try
        {
            // Parse the email
            var email = await _msgParser.ParseAsync(filePath);
            System.Console.WriteLine($"  Email type detected: {email.EmailType}");
            System.Console.WriteLine($"  Subject: {email.Subject}");
            System.Console.WriteLine($"  From: {email.From}");

            // Detect correspondences using MSG-specific detector
            var correspondences = _msgCorrespondenceDetector.DetectCorrespondences(email);
            System.Console.WriteLine($"  Found {correspondences.Count} correspondence(s)");

            if (correspondences.Count == 0)
            {
                System.Console.WriteLine("  No correspondences detected, skipping.");
                return 0;
            }

            // Create output folder for MSG method (no suffix)
            var outputFolder = _outputManager.CreateEmailFolder(filePath);
            System.Console.WriteLine($"  Output folder: {outputFolder}");

            // Copy the original parent email
            _outputManager.CopyParentEmail(filePath, outputFolder);
            System.Console.WriteLine("  Copied parent email");

            // Save each correspondence as PDF (from MSG parsing)
            foreach (var correspondence in correspondences)
            {
                await _outputManager.SaveCorrespondenceAsync(correspondence, outputFolder);
                System.Console.WriteLine($"  Saved correspondence {correspondence.Index + 1} (from MSG): {correspondence.From}");
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

    /// <summary>
    /// Process a single PDF file and return the number of correspondences found
    /// </summary>
    public async Task<int> ProcessPdfEmailAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        System.Console.WriteLine($"Processing PDF: {fileName}");

        try
        {
            // Parse the PDF email
            var email = await _pdfParser.ParseAsync(filePath);
            System.Console.WriteLine($"  Subject: {email.Subject}");
            System.Console.WriteLine($"  From: {email.From}");

            // Detect correspondences using PDF-specific detector (splits by "From:")
            var correspondences = _pdfCorrespondenceDetector.DetectCorrespondences(email);
            System.Console.WriteLine($"  Found {correspondences.Count} correspondence(s)");

            if (correspondences.Count == 0)
            {
                System.Console.WriteLine("  No correspondences detected, skipping.");
                return 0;
            }

            // Create output folder for OLD PDF method with "_pdf" suffix
            var outputFolderOld = _outputManager.CreateEmailFolder(filePath, "_pdf");
            System.Console.WriteLine($"  Output folder (old method): {outputFolderOld}");

            // Copy the original parent PDF
            _outputManager.CopyParentEmail(filePath, outputFolderOld);
            System.Console.WriteLine("  Copied parent PDF");

            // Save each correspondence as PDF with OLD solution (HTML-to-PDF conversion)
            System.Console.WriteLine("  Saving correspondences using old solution (HTML-to-PDF)...");
            foreach (var correspondence in correspondences)
            {
                await _outputManager.SaveCorrespondenceAsync(correspondence, outputFolderOld);
                System.Console.WriteLine($"  Saved correspondence {correspondence.Index + 1} (old): {correspondence.From}");
            }

            // Create output folder for NEW PDF method with "_pdf_new" suffix
            var outputFolderNew = _outputManager.CreateEmailFolder(filePath, "_pdf_new");
            System.Console.WriteLine($"  Output folder (new method): {outputFolderNew}");

            // Copy the original parent PDF to new folder
            _outputManager.CopyParentEmail(filePath, outputFolderNew);

            // NEW: Split PDF directly by "From:" sections and save in separate folder
            System.Console.WriteLine("  Extracting correspondences directly from PDF (new solution)...");
            var directSplitCount = await _pdfDirectSplitter.SplitPdfByCorrespondencesAsync(filePath, outputFolderNew, email);
            System.Console.WriteLine($"  Extracted {directSplitCount} correspondence(s) using direct PDF splitting");

            System.Console.WriteLine($"  Successfully processed {fileName}\n");
            return correspondences.Count;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"  Error processing {fileName}: {ex.Message}\n");
            return 0;
        }
    }

    /// <summary>
    /// Process a single file (auto-detect MSG or PDF)
    /// </summary>
    public async Task<int> ProcessEmailAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".msg" => await ProcessMsgEmailAsync(filePath),
            ".pdf" => await ProcessPdfEmailAsync(filePath),
            _ => throw new NotSupportedException($"File type '{extension}' is not supported. Only .msg and .pdf files are supported.")
        };
    }
}
