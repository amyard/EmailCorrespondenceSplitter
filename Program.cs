using EmailCorrespondenceSplitter.Services;
using System.Text;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("=== Email Correspondence Splitter ===\n");

// Register code page encoding provider (required for MSG file parsing)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Setup services
var emailParser = new MsgEmailParser();
var correspondenceDetector = new CorrespondenceDetector();
var outputManager = new OutputManager("Output");
var emailSplitter = new EmailSplitter(emailParser, correspondenceDetector, outputManager);

// Get all MSG files from Assets folder
var assetsFolder = "Assets";
if (!Directory.Exists(assetsFolder))
{
    Console.WriteLine($"Error: Assets folder not found at '{assetsFolder}'");
    return;
}

var msgFiles = Directory.GetFiles(assetsFolder, "*.msg", SearchOption.AllDirectories);

if (msgFiles.Length == 0)
{
    Console.WriteLine("No MSG files found in Assets folder.");
    return;
}

Console.WriteLine($"Found {msgFiles.Length} MSG file(s) to process.\n");

// Process all email files
await emailSplitter.ProcessEmailsAsync(msgFiles);

Console.WriteLine("\n=== Processing Complete ===");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
