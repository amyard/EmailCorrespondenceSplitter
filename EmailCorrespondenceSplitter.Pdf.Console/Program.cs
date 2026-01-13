using EmailCorrespondenceSplitter.Pdf.Console.Services;

Console.WriteLine("Email Correspondence Splitter - PDF Generator");
Console.WriteLine("==============================================\n");

// Get the Assets directory (should be copied to output during build)
var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
var outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output");

// Create the service and process all emails
var splitterService = new EmailCorrespondenceSplitterService();
splitterService.ProcessAllEmailsInDirectory(assetsDirectory, outputDirectory);

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();