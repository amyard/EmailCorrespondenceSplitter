using System.Text;
using EmailCorrespondenceSplitter.Pdf.Console.Services;

// Register code pages for proper encoding support (required for MsgReader)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Console.WriteLine("===========================================");
Console.WriteLine("   Email Correspondence Splitter");
Console.WriteLine("===========================================\n");

// Get the Assets directory (should be copied to output during build)
var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
var outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output");

Console.WriteLine($"Assets directory: {assetsDirectory}");
Console.WriteLine($"Output directory: {outputDirectory}\n");

// Create and run the email splitter
var splitter = new EmailSplitter(outputDirectory);
await splitter.ProcessDirectoryAsync(assetsDirectory);

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();