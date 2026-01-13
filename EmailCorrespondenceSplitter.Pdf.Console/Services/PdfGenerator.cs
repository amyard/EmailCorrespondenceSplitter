using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.IO.Font.Constants;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Service to generate PDF files from email correspondences using iText7
/// </summary>
public class PdfGenerator
{
    /// <summary>
    /// Generate a PDF file for a single correspondence
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

            // Set up fonts
            var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var regularFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            // Title
            var title = new Paragraph($"Correspondence #{correspondence.Index + 1}")
                .SetFont(boldFont)
                .SetFontSize(16)
                .SetFontColor(ColorConstants.BLUE)
                .SetMarginBottom(20);
            document.Add(title);

            // Subject
            AddLabelValuePair(document, "Subject:", correspondence.Subject, boldFont, regularFont);

            // From
            AddLabelValuePair(document, "From:", correspondence.From, boldFont, regularFont);

            // To
            AddLabelValuePair(document, "To:", correspondence.To, boldFont, regularFont);

            // Date
            if (correspondence.SentDate.HasValue)
            {
                AddLabelValuePair(document, "Date:", 
                    correspondence.SentDate.Value.ToString("yyyy-MM-dd HH:mm:ss"), 
                    boldFont, regularFont);
            }

            // Separator line
            var separator = new Paragraph()
                .SetMarginTop(15)
                .SetMarginBottom(15)
                .SetBorderTop(new iText.Layout.Borders.SolidBorder(ColorConstants.LIGHT_GRAY, 1));
            document.Add(separator);

            // Content
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
    /// Helper method to add label-value pairs to the document
    /// </summary>
    private void AddLabelValuePair(Document document, string label, string value, 
        PdfFont boldFont, PdfFont regularFont)
    {
        var labelParagraph = new Paragraph(label)
            .SetFont(boldFont)
            .SetFontSize(11)
            .SetMarginBottom(2);
        document.Add(labelParagraph);

        var valueParagraph = new Paragraph(value)
            .SetFont(regularFont)
            .SetFontSize(11)
            .SetMarginBottom(10);
        document.Add(valueParagraph);
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
