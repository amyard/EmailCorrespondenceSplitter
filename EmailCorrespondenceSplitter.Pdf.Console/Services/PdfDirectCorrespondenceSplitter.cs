using System.Text;
using System.Text.RegularExpressions;
using EmailCorrespondenceSplitter.Pdf.Console.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Directly splits PDF files into separate PDF correspondences by detecting "From:" sections.
/// Preserves the original PDF content without any HTML conversion.
/// Maps text-based correspondence detection to PDF page boundaries.
/// Uses the SAME text source as PdfCorrespondenceDetector for identical results.
/// </summary>
public class PdfDirectCorrespondenceSplitter
{
    // Multi-language patterns for "From:" field (same as PdfCorrespondenceDetector)
    private static readonly string[] FromPatterns =
    [
        "From",      // English
        "Von",       // German
        "De",        // French, Spanish, Portuguese
        "Da",        // Italian
        "??",        // Russian
        "Od",        // Polish, Czech
        "Från",      // Swedish
        "Fra",       // Norwegian, Danish
        "???",    // Japanese
        "????",   // Korean
        "???",    // Chinese Simplified
        "???",    // Chinese Traditional
    ];

    /// <summary>
    /// Split a PDF file into separate correspondence PDF files based on "From:" sections
    /// </summary>
    /// <param name="inputPdfPath">Path to the input PDF file</param>
    /// <param name="outputFolder">Folder to save the split PDF files (e.g., em1_pdf_new folder)</param>
    /// <param name="parsedEmail">The already-parsed email from PdfEmailParser (to use same text as OLD method)</param>
    /// <returns>Number of correspondences found and saved</returns>
    public async Task<int> SplitPdfByCorrespondencesAsync(string inputPdfPath, string outputFolder, EmailMessage parsedEmail)
    {
        return await Task.Run(() => SplitPdfByCorrespondences(inputPdfPath, outputFolder, parsedEmail));
    }

    /// <summary>
    /// Split a PDF file into separate correspondence PDF files based on "From:" sections (synchronous)
    /// </summary>
    private int SplitPdfByCorrespondences(string inputPdfPath, string outputFolder, EmailMessage parsedEmail)
    {
        try
        {
            using var reader = new PdfReader(inputPdfPath);
            using var sourcePdf = new PdfDocument(reader);

            // Use the SAME text that the OLD detector uses (from parsedEmail.TextBody)
            var fullText = parsedEmail.TextBody;
            
            // Get page ranges from the parsed email (same as OLD method)
            var pageTextRanges = GetPageRanges(parsedEmail);

            if (string.IsNullOrWhiteSpace(fullText))
            {
                System.Console.WriteLine("  No text content found in PDF");
                return 0;
            }

            System.Console.WriteLine($"  DEBUG NEW: Using email.TextBody with {fullText.Length} characters");
            System.Console.WriteLine($"  DEBUG NEW: Page ranges from email: {pageTextRanges.Count}");
            
            // Print page ranges for debugging
            foreach (var pr in pageTextRanges)
            {
                System.Console.WriteLine($"  DEBUG NEW: Page {pr.PageNumber}: chars {pr.StartIndex}-{pr.EndIndex}");
            }

            // Find correspondence boundaries in the text (same logic as PdfCorrespondenceDetector)
            var correspondenceBoundaries = FindCorrespondenceBoundariesInText(fullText);

            System.Console.WriteLine($"  DEBUG NEW: Found {correspondenceBoundaries.Count} correspondence boundaries:");
            for (int i = 0; i < correspondenceBoundaries.Count; i++)
            {
                var (start, end) = correspondenceBoundaries[i];
                System.Console.WriteLine($"  DEBUG NEW: Correspondence {i + 1}: chars {start}-{end} (length: {end - start})");
                
                // Show first 100 chars of each correspondence
                var preview = fullText.Substring(start, Math.Min(100, end - start)).Replace("\r", "").Replace("\n", " ");
                System.Console.WriteLine($"  DEBUG NEW: Preview: {preview}...");
            }

            if (correspondenceBoundaries.Count <= 1)
            {
                System.Console.WriteLine("  No 'From:' sections found or only single correspondence");
                return 0;
            }

            System.Console.WriteLine($"  Found {correspondenceBoundaries.Count} correspondence(s) in text");

            // Map text boundaries to page ranges
            var pageRanges = MapTextBoundariesToPages(correspondenceBoundaries, pageTextRanges, sourcePdf.GetNumberOfPages());

            System.Console.WriteLine($"  DEBUG NEW: Mapped to {pageRanges.Count} page ranges:");
            for (int i = 0; i < pageRanges.Count; i++)
            {
                var (startPage, endPage) = pageRanges[i];
                System.Console.WriteLine($"  DEBUG NEW: Correspondence {i + 1}: pages {startPage}-{endPage}");
            }

            // Split PDF into separate files based on page ranges
            for (int i = 0; i < pageRanges.Count; i++)
            {
                var (startPage, endPage) = pageRanges[i];
                ExtractPdfPages(inputPdfPath, outputFolder, startPage, endPage, i);
            }

            return pageRanges.Count;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"  Error splitting PDF: {ex.Message}");
            System.Console.WriteLine($"  Stack trace: {ex.StackTrace}");
            return 0;
        }
    }

    /// <summary>
    /// Get page ranges from email custom data (same as PdfCorrespondenceDetector)
    /// </summary>
    private List<(int PageNumber, int StartIndex, int EndIndex)> GetPageRanges(EmailMessage email)
    {
        if (email.CustomData.TryGetValue("PageTextRanges", out var rangesObj) && 
            rangesObj is List<(int PageNumber, int StartIndex, int EndIndex)> ranges)
        {
            return ranges;
        }
        return [];
    }

    /// <summary>
    /// Find correspondence boundaries in text (same logic as PdfCorrespondenceDetector.SplitWithPositions)
    /// </summary>
    private List<(int StartIndex, int EndIndex)> FindCorrespondenceBoundariesInText(string text)
    {
        var boundaries = new List<(int StartIndex, int EndIndex)>();
        
        // Build the split pattern for "From:" in multiple languages (same as OLD)
        var fromPatternString = string.Join("|", FromPatterns.Select(Regex.Escape));
        var splitPattern = $@"(?=^\s*(?:{fromPatternString}):\s*.+$)";
        
        var matches = Regex.Matches(text, splitPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        
        if (matches.Count == 0)
        {
            // No splits found, return entire text as one correspondence
            boundaries.Add((0, text.Length));
            return boundaries;
        }

        // Get split indices from matches
        var splitIndices = matches.Cast<Match>().Select(m => m.Index).ToList();
        
        // Create boundaries between split points (same as OLD)
        for (int i = 0; i < splitIndices.Count; i++)
        {
            int start = splitIndices[i];
            int end = (i + 1 < splitIndices.Count) ? splitIndices[i + 1] : text.Length;
            
            boundaries.Add((start, end));
        }

        return boundaries;
    }

    /// <summary>
    /// Map text character boundaries to PDF page ranges
    /// </summary>
    private List<(int StartPage, int EndPage)> MapTextBoundariesToPages(
        List<(int StartIndex, int EndIndex)> textBoundaries,
        List<(int PageNumber, int StartIndex, int EndIndex)> pageTextRanges,
        int totalPages)
    {
        var pageRanges = new List<(int StartPage, int EndPage)>();

        foreach (var (textStart, textEnd) in textBoundaries)
        {
            // Find the first page that contains or starts after the text start
            int startPage = 1;
            for (int i = 0; i < pageTextRanges.Count; i++)
            {
                var pageRange = pageTextRanges[i];
                // If text starts within this page's range or before the page starts
                if (textStart >= pageRange.StartIndex && textStart < pageRange.EndIndex)
                {
                    startPage = pageRange.PageNumber;
                    break;
                }
                // If text starts before this page but we haven't found a match yet
                else if (textStart < pageRange.StartIndex)
                {
                    startPage = Math.Max(1, pageRange.PageNumber - 1);
                    break;
                }
            }

            // Find the last page that contains the text end
            int endPage = totalPages;
            for (int i = 0; i < pageTextRanges.Count; i++)
            {
                var pageRange = pageTextRanges[i];
                // If text ends within this page's range
                if (textEnd > pageRange.StartIndex && textEnd <= pageRange.EndIndex)
                {
                    endPage = pageRange.PageNumber;
                    break;
                }
                // If text ends before this page starts
                else if (textEnd <= pageRange.StartIndex && i > 0)
                {
                    endPage = pageTextRanges[i - 1].PageNumber;
                    break;
                }
            }

            // Ensure we have at least one page
            if (endPage < startPage)
            {
                endPage = startPage;
            }

            pageRanges.Add((startPage, endPage));
        }

        return pageRanges;
    }

    /// <summary>
    /// Extract specific pages from source PDF and save to a new PDF file
    /// </summary>
    private void ExtractPdfPages(string sourcePdfPath, string outputFolder, int startPage, int endPage, int correspondenceIndex)
    {
        try
        {
            var fileName = $"{(correspondenceIndex + 1):D2}_correspondence.pdf";
            var outputPath = Path.Combine(outputFolder, fileName);

            using var sourceReader = new PdfReader(sourcePdfPath);
            using var sourcePdf = new PdfDocument(sourceReader);
            using var writer = new PdfWriter(outputPath);
            using var targetPdf = new PdfDocument(writer);

            // Copy pages from source to target
            sourcePdf.CopyPagesTo(startPage, endPage, targetPdf);

            System.Console.WriteLine($"  Saved new correspondence {correspondenceIndex + 1} (pages {startPage}-{endPage}): {fileName}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"  Error extracting pages {startPage}-{endPage}: {ex.Message}");
        }
    }
}
