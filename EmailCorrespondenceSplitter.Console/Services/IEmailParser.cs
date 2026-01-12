namespace EmailCorrespondenceSplitter.Services;

/// <summary>
/// Interface for parsing email files and extracting their content
/// </summary>
public interface IEmailParser
{
    /// <summary>
    /// Parse an email file and extract its content
    /// </summary>
    /// <param name="filePath">Path to the email file</param>
    /// <returns>Parsed email message</returns>
    Task<Models.EmailMessage> ParseAsync(string filePath);
    
    /// <summary>
    /// Check if the parser supports the given file
    /// </summary>
    /// <param name="filePath">Path to the email file</param>
    /// <returns>True if the file type is supported</returns>
    bool CanParse(string filePath);
}
