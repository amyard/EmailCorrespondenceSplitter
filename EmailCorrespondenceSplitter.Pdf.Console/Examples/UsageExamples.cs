using EmailCorrespondenceSplitter.Pdf.Console.Services;

namespace EmailCorrespondenceSplitter.Pdf.Console.Examples;

/// <summary>
/// Examples of how to use the Email Correspondence Splitter
/// </summary>
public static class UsageExamples
{
    /// <summary>
    /// Example 1: Process all emails in the Assets directory
    /// Each email will get its own folder in Output/
    /// </summary>
    public static void ProcessAllEmailsInAssets()
    {
        var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output");

        var service = new EmailCorrespondenceSplitterService();
        service.ProcessAllEmailsInDirectory(assetsDirectory, outputDirectory);
        
        // Result: Output/em1/, Output/em2/, etc., each containing correspondence PDFs
    }

    /// <summary>
    /// Example 2: Process a single email file
    /// Creates a folder Output/em1/ with correspondence PDFs inside
    /// </summary>
    public static void ProcessSingleEmail()
    {
        var msgFilePath = Path.Combine(AppContext.BaseDirectory, "Assets", "em1.msg");
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output");

        var service = new EmailCorrespondenceSplitterService();
        service.ProcessEmail(msgFilePath, outputDirectory);
        
        // Result: Output/em1/correspondence_1.pdf, Output/em1/correspondence_2.pdf, etc.
    }

    /// <summary>
    /// Example 3: Process emails from a custom directory
    /// Each email gets its own folder
    /// </summary>
    public static void ProcessCustomDirectory()
    {
        var inputDirectory = @"C:\MyEmails";
        var outputDirectory = @"C:\MyEmails\PDFs";

        var service = new EmailCorrespondenceSplitterService();
        service.ProcessAllEmailsInDirectory(inputDirectory, outputDirectory);
        
        // Result: C:\MyEmails\PDFs\email1\, C:\MyEmails\PDFs\email2\, etc.
    }

    /// <summary>
    /// Example 4: Manually extract correspondences from text
    /// </summary>
    public static void ManualExtractionExample()
    {
        var extractor = new CorrespondenceExtractor();
        
        var emailContent = @"
From: john@example.com
To: jane@example.com
Sent: Monday, January 1, 2024 10:00 AM
Subject: Re: Meeting

Hi Jane,

Thanks for the update!

Best regards,
John

From: jane@example.com
To: john@example.com
Sent: Monday, January 1, 2024 9:00 AM
Subject: Meeting

Hi John,

Here's the information you requested.

Thanks,
Jane
";

        var correspondences = extractor.ExtractCorrespondences(emailContent, "Re: Meeting");
        
        System.Console.WriteLine($"Found {correspondences.Count} correspondences:");
        foreach (var corr in correspondences)
        {
            System.Console.WriteLine($"  - From: {corr.From}, To: {corr.To}");
        }
    }

    /// <summary>
    /// Example 5: Generate PDFs with custom folder structure
    /// </summary>
    public static void CustomPdfGenerationExample()
    {
        // Read email
        var reader = new EmailReader();
        var (subject, textBody) = reader.ReadMsgFile("Assets/em1.msg");

        // Extract correspondences
        var extractor = new CorrespondenceExtractor();
        var correspondences = extractor.ExtractCorrespondences(textBody, subject);

        // Generate PDFs in a custom folder structure
        var pdfGenerator = new PdfGenerator();
        pdfGenerator.GeneratePdfs(correspondences, "Output", "my_custom_email_folder");
        
        // Result: Output/my_custom_email_folder/correspondence_1.pdf, etc.
    }

    /// <summary>
    /// Example 6: Process with organized output structure
    /// </summary>
    public static void OrganizedOutputExample()
    {
        var service = new EmailCorrespondenceSplitterService();
        
        // All emails from Assets will be organized into separate folders
        // Output/em1/, Output/em2/, Output/em3/, etc.
        service.ProcessAllEmailsInDirectory("Assets", "Output");
        
        System.Console.WriteLine("\nFolder structure created:");
        System.Console.WriteLine("Output/");
        System.Console.WriteLine("  ??? em1/");
        System.Console.WriteLine("  ?   ??? correspondence_1.pdf");
        System.Console.WriteLine("  ?   ??? correspondence_2.pdf");
        System.Console.WriteLine("  ??? em2/");
        System.Console.WriteLine("  ?   ??? correspondence.pdf");
        System.Console.WriteLine("  ??? em3/");
        System.Console.WriteLine("      ??? correspondence_1.pdf");
        System.Console.WriteLine("      ??? correspondence_2.pdf");
        System.Console.WriteLine("      ??? correspondence_3.pdf");
    }
}
