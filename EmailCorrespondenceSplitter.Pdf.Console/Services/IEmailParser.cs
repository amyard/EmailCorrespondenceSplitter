using EmailCorrespondenceSplitter.Pdf.Console.Models;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Interface for email parsing
/// </summary>
public interface IEmailParser
{
    Task<EmailMessage> ParseAsync(string filePath);
    bool CanParse(string filePath);
}
