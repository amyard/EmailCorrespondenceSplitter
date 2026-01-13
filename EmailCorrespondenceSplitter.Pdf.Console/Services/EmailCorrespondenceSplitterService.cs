namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Main service that orchestrates email correspondence extraction and PDF generation
/// </summary>
public class EmailCorrespondenceSplitterService
{
    private readonly EmailReader _emailReader;
    private readonly CorrespondenceExtractor _correspondenceExtractor;
    private readonly PdfGenerator _pdfGenerator;

    public EmailCorrespondenceSplitterService()
    {
        _emailReader = new EmailReader();
        _correspondenceExtractor = new CorrespondenceExtractor();
        _pdfGenerator = new PdfGenerator();
    }

    /// <summary>
    /// Process a single MSG file: extract correspondences and generate PDFs in a dedicated folder
    /// </summary>
    /// <param name="msgFilePath">Path to the MSG file</param>
    /// <param name="outputDirectory">Base directory where email folders should be created</param>
    public void ProcessEmail(string msgFilePath, string outputDirectory)
    {
        var fileName = Path.GetFileNameWithoutExtension(msgFilePath);
        System.Console.WriteLine($"\n{'='} Processing: {Path.GetFileName(msgFilePath)} {'='}");

        // Step 1: Read the MSG file
        var (subject, textBody) = _emailReader.ReadMsgFile(msgFilePath);
        
        System.Console.WriteLine($"  Subject: {(string.IsNullOrEmpty(subject) ? "[Empty]" : subject)}");
        System.Console.WriteLine($"  Body length: {textBody.Length} characters");
        
        if (string.IsNullOrEmpty(textBody))
        {
            System.Console.WriteLine("  ? Failed to read email content. Skipping.");
            return;
        }

        // Show first 200 characters of content for debugging
        if (textBody.Length > 0)
        {
            var preview = textBody.Length > 200 ? textBody.Substring(0, 200) + "..." : textBody;
            System.Console.WriteLine($"  Content preview: {preview.Replace("\n", " ").Replace("\r", "")}");
        }

        // Step 2: Extract correspondences
        var correspondences = _correspondenceExtractor.ExtractCorrespondences(textBody, subject);
        System.Console.WriteLine($"  Found {correspondences.Count} correspondence(s)");

        if (correspondences.Count > 0)
        {
            foreach (var corr in correspondences)
            {
                System.Console.WriteLine($"    - Correspondence #{corr.Index + 1}: From={corr.From}, To={corr.To}, Content length={corr.Content.Length}");
            }
        }

        // Step 3: Generate PDFs in a dedicated folder for this email
        _pdfGenerator.GeneratePdfs(correspondences, outputDirectory, fileName);
    }

    /// <summary>
    /// Process all MSG files in a directory
    /// Each email will get its own folder with correspondence PDFs inside
    /// </summary>
    /// <param name="inputDirectory">Directory containing MSG files</param>
    /// <param name="outputDirectory">Base directory where email folders will be created</param>
    public void ProcessAllEmailsInDirectory(string inputDirectory, string outputDirectory)
    {
        if (!Directory.Exists(inputDirectory))
        {
            System.Console.WriteLine($"Error: Input directory '{inputDirectory}' does not exist.");
            return;
        }

        var msgFiles = Directory.GetFiles(inputDirectory, "*.msg");
        
        if (msgFiles.Length == 0)
        {
            System.Console.WriteLine($"No MSG files found in '{inputDirectory}'");
            return;
        }

        System.Console.WriteLine($"\n{'='} Email Correspondence Splitter {'='}");
        System.Console.WriteLine($"Found {msgFiles.Length} MSG file(s) to process\n");
        System.Console.WriteLine($"Input directory:  {Path.GetFullPath(inputDirectory)}");
        System.Console.WriteLine($"Output directory: {Path.GetFullPath(outputDirectory)}");

        foreach (var msgFile in msgFiles)
        {
            ProcessEmail(msgFile, outputDirectory);
        }

        System.Console.WriteLine($"\n{'='} Processing Complete {'='}");
        System.Console.WriteLine($"Output directory: {Path.GetFullPath(outputDirectory)}");
    }
}
