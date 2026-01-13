using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Font;
using iText.IO.Font.Constants;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Service to generate PDF files from email correspondences using iText7
/// PDF contains only the raw email content without extra formatting or labels
/// </summary>
public class PdfGenerator
{
    /// <summary>
    /// Generate a PDF file for a single correspondence
    /// Contains only the email content as it appears in the original message
    /// </summary>
    /// <param name="correspondence">The correspondence to convert to PDF</param>
    /// <param name="outputPath">Path where the PDF should be saved</param>
    public void GeneratePdf(Models.EmailCorrespondence correspondence, string outputPath)
    {
        PdfWriter? writer = null;
        PdfDocument? pdf = null;
        Document? document = null;
        
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            writer = new PdfWriter(outputPath);
            pdf = new PdfDocument(writer);
            document = new Document(pdf);

            // Set up font - simple regular font for plain text
            var regularFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            // Add only the email content - exactly as it appears in the original
            var content = new Paragraph(correspondence.Content)
                .SetFont(regularFont)
                .SetFontSize(10)
                .SetFixedLeading(15);
            document.Add(content);

            // Close document properly to flush content to file
            document.Close();
            document = null;
            pdf.Close();
            pdf = null;
            writer.Close();
            writer = null;

            System.Console.WriteLine($"  ? Generated: {Path.GetFileName(outputPath)}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"  ? Error generating PDF for correspondence #{correspondence.Index + 1}: {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Console.WriteLine($"     Inner exception: {ex.InnerException.Message}");
            }
        }
        finally
        {
            // Ensure resources are disposed even if Close() wasn't called
            try { document?.Close(); } catch { }
            try { pdf?.Close(); } catch { }
            try { writer?.Close(); } catch { }
        }
    }

    /// <summary>
    /// Generate PDF files for all correspondences in a list
    /// Creates a folder for the email and stores all correspondence PDFs inside
    /// </summary>
    /// <param name="correspondences">List of correspondences</param>
    /// <param name="outputDirectory">Base output directory</param>
    /// <param name="baseFileName">Base name for the email (will be used as folder name)</param>
    public void GeneratePdfs(List<Models.EmailCorrespondence> correspondences, string outputDirectory, string baseFileName)
    {
        if (correspondences == null || correspondences.Count == 0)
        {
            System.Console.WriteLine($"  ? No correspondences to generate for '{baseFileName}'");
            return;
        }

        // Create a folder for this email
        var emailFolderPath = Path.Combine(outputDirectory, baseFileName);
        Directory.CreateDirectory(emailFolderPath);

        System.Console.WriteLine($"\nGenerating {correspondences.Count} PDF file(s) in folder '{baseFileName}/':");

        foreach (var correspondence in correspondences)
        {
            // Create filename: correspondence_1.pdf, correspondence_2.pdf, etc.
            var fileName = correspondences.Count == 1 
                ? "correspondence.pdf"
                : $"correspondence_{correspondence.Index + 1}.pdf";
            
            var outputPath = Path.Combine(emailFolderPath, fileName);
            
            GeneratePdf(correspondence, outputPath);
        }

        System.Console.WriteLine($"  Folder location: {Path.GetFullPath(emailFolderPath)}");
    }
}
