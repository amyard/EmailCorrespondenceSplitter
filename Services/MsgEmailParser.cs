using MsgReader.Outlook;
using EmailCorrespondenceSplitter.Models;

namespace EmailCorrespondenceSplitter.Services;

/// <summary>
/// Parser for MSG (Outlook) email files
/// </summary>
public class MsgEmailParser : IEmailParser
{
    /// <summary>
    /// Parse a MSG file and extract email content
    /// </summary>
    public async Task<EmailMessage> ParseAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            using var msg = new Storage.Message(filePath);
            
            // Get recipients
            var toRecipients = new List<string>();
            var ccRecipients = new List<string>();
            
            if (msg.Recipients != null)
            {
                foreach (Storage.Recipient recipient in msg.Recipients)
                {
                    var recipientEmail = recipient.Email ?? recipient.DisplayName ?? string.Empty;
                    
                    // MsgReader uses DisplayType property for recipient classification
                    // Type property contains RecipientType enum
                    try
                    {
                        // Check the Type property - it should be RecipientType enum
                        var typeString = recipient.Type.ToString();
                        
                        if (typeString.Contains("To", StringComparison.OrdinalIgnoreCase))
                        {
                            toRecipients.Add(recipientEmail);
                        }
                        else if (typeString.Contains("Cc", StringComparison.OrdinalIgnoreCase))
                        {
                            ccRecipients.Add(recipientEmail);
                        }
                    }
                    catch
                    {
                        // Default to To if type detection fails
                        toRecipients.Add(recipientEmail);
                    }
                }
            }
            
            // Extract body with error handling for RTF parsing issues
            string htmlBody = string.Empty;
            string textBody = string.Empty;
            
            try
            {
                htmlBody = msg.BodyHtml ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: Could not extract HTML body: {ex.Message}");
            }
            
            try
            {
                textBody = msg.BodyText ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: Could not extract text body: {ex.Message}");
            }
            
            // If both failed, try to extract RTF body
            if (string.IsNullOrEmpty(htmlBody) && string.IsNullOrEmpty(textBody))
            {
                try
                {
                    var rtfBody = msg.BodyRtf ?? string.Empty;
                    if (!string.IsNullOrEmpty(rtfBody))
                    {
                        // Use RTF as fallback - it will be plain text representation
                        textBody = "RTF content available but could not be converted";
                        Console.WriteLine("  Warning: Only RTF body available, HTML/Text conversion failed");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Warning: Could not extract RTF body: {ex.Message}");
                }
            }
            
            var emailMessage = new EmailMessage
            {
                Subject = msg.Subject ?? string.Empty,
                From = msg.Sender?.Email ?? msg.Sender?.DisplayName ?? string.Empty,
                To = string.Join("; ", toRecipients),
                Cc = string.Join("; ", ccRecipients),
                SentOn = msg.SentOn.HasValue ? msg.SentOn.Value.DateTime : null,
                HtmlBody = htmlBody,
                TextBody = textBody,
                FilePath = filePath,
                EmailType = DetectEmailType(msg)
            };
            
            // Extract attachment names
            if (msg.Attachments != null && msg.Attachments.Count > 0)
            {
                foreach (var attachment in msg.Attachments)
                {
                    if (attachment is Storage.Attachment att)
                    {
                        emailMessage.Attachments.Add(att.FileName ?? "Unnamed Attachment");
                    }
                }
            }
            
            return emailMessage;
        });
    }
    
    /// <summary>
    /// Check if the file is a MSG file
    /// </summary>
    public bool CanParse(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".msg", StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Detect email client type from message headers and content
    /// </summary>
    private EmailType DetectEmailType(Storage.Message msg)
    {
        var htmlBody = msg.BodyHtml ?? string.Empty;
        
        // Check for Gmail indicators
        if (htmlBody.Contains("gmail_quote", StringComparison.OrdinalIgnoreCase) ||
            htmlBody.Contains("gmail_signature", StringComparison.OrdinalIgnoreCase))
        {
            return EmailType.Gmail;
        }
        
        // Check for Apple Mail indicators
        if (htmlBody.Contains("Apple-interchange-newline", StringComparison.OrdinalIgnoreCase) ||
            htmlBody.Contains("AppleMailSignature", StringComparison.OrdinalIgnoreCase))
        {
            return EmailType.Apple;
        }
        
        // Check for Outlook indicators
        if (htmlBody.Contains("MsoNormal", StringComparison.OrdinalIgnoreCase) ||
            htmlBody.Contains("WordSection", StringComparison.OrdinalIgnoreCase))
        {
            return EmailType.Outlook;
        }
        
        // Check headers if available
        if (msg.Headers != null)
        {
            var headerText = msg.Headers.ToString() ?? string.Empty;
            
            if (headerText.Contains("X-Google", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.Gmail;
            }
            
            if (headerText.Contains("X-Mailer", StringComparison.OrdinalIgnoreCase))
            {
                if (headerText.Contains("Apple", StringComparison.OrdinalIgnoreCase))
                {
                    return EmailType.Apple;
                }
                if (headerText.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                {
                    return EmailType.Outlook;
                }
            }
        }
        
        // Default to Outlook for MSG files if no other indicators found
        return EmailType.Outlook;
    }
}
