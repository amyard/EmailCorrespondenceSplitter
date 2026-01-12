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
        var headerText = msg.Headers?.ToString() ?? string.Empty;
        
        // Priority 1: Check headers first (most reliable)
        if (!string.IsNullOrEmpty(headerText))
        {
            // Gmail
            if (headerText.Contains("X-Google", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("X-Gm-Message-State", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.Gmail;
            }
            
            // Office 365
            if (headerText.Contains("Microsoft.Exchange.Transport", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("X-MS-Exchange", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.Office365;
            }
            
            // ProtonMail
            if (headerText.Contains("X-Pm-", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("protonmail", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.ProtonMail;
            }
            
            // Thunderbird
            if (headerText.Contains("Thunderbird", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("Mozilla/5.0", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.Thunderbird;
            }
            
            // Yahoo Mail
            if (headerText.Contains("YMailISG", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("X-Yahoo", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.YahooMail;
            }
            
            // Zoho Mail
            if (headerText.Contains("X-Zoho", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("zoho.com", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.ZohaMail;
            }
            
            // X-Mailer header detection
            if (headerText.Contains("X-Mailer", StringComparison.OrdinalIgnoreCase))
            {
                if (headerText.Contains("Apple", StringComparison.OrdinalIgnoreCase))
                {
                    return EmailType.Apple;
                }
                if (headerText.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                {
                    if (headerText.Contains("Office 365", StringComparison.OrdinalIgnoreCase))
                        return EmailType.Office365;
                    return EmailType.Outlook;
                }
            }
        }
        
        // Priority 2: Check HTML body patterns
        if (!string.IsNullOrEmpty(htmlBody))
        {
            // Gmail indicators
            if (htmlBody.Contains("gmail_quote", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("gmail_signature", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("gmail_attr", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.Gmail;
            }
            
            // Apple Mail indicators
            if (htmlBody.Contains("Apple-interchange-newline", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("AppleMailSignature", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("webkit-html-composer-wrapper", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.Apple;
            }
            
            // Outlook/Office 365 indicators
            if (htmlBody.Contains("MsoNormal", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("WordSection", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("OutlookMessageHeader", StringComparison.OrdinalIgnoreCase))
            {
                // Try to distinguish between Outlook and Office 365
                if (htmlBody.Contains("safelink.protection.outlook.com", StringComparison.OrdinalIgnoreCase) ||
                    htmlBody.Contains("outlook.office365.com", StringComparison.OrdinalIgnoreCase))
                {
                    return EmailType.Office365;
                }
                return EmailType.Outlook;
            }
            
            // Outlook Web (OWA)
            if (htmlBody.Contains("OWALink", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("x_x_", StringComparison.OrdinalIgnoreCase)) // O365 name mangling
            {
                return EmailType.OutlookWeb;
            }
            
            // Thunderbird
            if (htmlBody.Contains("moz-signature", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("moz-cite-prefix", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.Thunderbird;
            }
            
            // Yahoo Mail
            if (htmlBody.Contains("yahoo-style-wrap", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("yiv", StringComparison.OrdinalIgnoreCase)) // Yahoo's class prefix
            {
                return EmailType.YahooMail;
            }
            
            // ProtonMail
            if (htmlBody.Contains("protonmail_quote", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("protonmail_signature", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.ProtonMail;
            }
            
            // Zoho Mail
            if (htmlBody.Contains("zmail_", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("zoho_mail", StringComparison.OrdinalIgnoreCase))
            {
                return EmailType.ZohaMail;
            }
        }
        
        // Priority 3: Default based on file type
        // MSG files are typically from Outlook/Exchange ecosystem
        return EmailType.Outlook;
    }
}
