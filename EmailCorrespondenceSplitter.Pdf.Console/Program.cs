using System.Text;
using EmailCorrespondenceSplitter.Pdf.Console.Services;

// Register code pages for proper encoding support (required for MsgReader)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Console.WriteLine("===========================================");
Console.WriteLine("   Email Correspondence Splitter (PDF)");
Console.WriteLine("===========================================\n");

// Get the Assets directory (should be copied to output during build)
var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
var outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output");

Console.WriteLine($"Assets directory: {assetsDirectory}");
Console.WriteLine($"Output directory: {outputDirectory}\n");

// Create and run the PDF email splitter (supports both MSG and PDF files)
var splitter = new PdfEmailSplitter(outputDirectory);
await splitter.ProcessDirectoryAsync(assetsDirectory);

// Summary of expected vs actual results
Console.WriteLine("\n===========================================");
Console.WriteLine("   Processing Summary");
Console.WriteLine("===========================================");

// Expected results for MSG files
var expectedMsgResults = new Dictionary<string, int>
{
    { "em1.msg", 2 },  // Outlook with 2 correspondences
    { "em2.msg", 2 },  // Outlook with 2 correspondences
    { "em3.msg", 8 },  // Outlook with 8 correspondences and images
    { "em4.msg", 24 }, // Outlook with 24 forwarded correspondences
    { "em5.msg", 2 },  // Apple with 2 correspondences
    { "em6.msg", 4 },  // Outlook with 4 correspondences
};

// Expected results for PDF files
var expectedPdfResults = new Dictionary<string, int>
{
    { "em1_pdf.pdf", 2 },  // Outlook with 2 correspondences
    { "em3_pdf.pdf", 8 },  // Outlook with 8 correspondences and images
    { "em4_pdf.pdf", 24 }, // Outlook with 24 forwarded correspondences
    { "em5_pdf.pdf", 2 },  // Apple with 2 correspondences
    { "em6_pdf.pdf", 4 },  // Outlook with 4 correspondences
};

Console.WriteLine("\nMSG Files:");
foreach (var expected in expectedMsgResults)
{
    var emailPath = Path.Combine(assetsDirectory, expected.Key);
    if (File.Exists(emailPath))
    {
        Console.WriteLine($"  {expected.Key}: Expected {expected.Value} correspondence(s)");
    }
}

Console.WriteLine("\nPDF Files:");
foreach (var expected in expectedPdfResults)
{
    var emailPath = Path.Combine(assetsDirectory, expected.Key);
    if (File.Exists(emailPath))
    {
        Console.WriteLine($"  {expected.Key}: Expected {expected.Value} correspondence(s)");
    }
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();