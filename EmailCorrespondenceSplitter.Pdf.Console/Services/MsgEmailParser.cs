using MsgReader.Outlook;
using EmailCorrespondenceSplitter.Pdf.Console.Models;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Parser for MSG (Outlook) email files
/// </summary>
public class MsgEmailParser : IEmailParser
{
    public async Task<EmailMessage> ParseAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            using var msg = new Storage.Message(filePath);

            var toRecipients = new List<string>();
            var ccRecipients = new List<string>();

            if (msg.Recipients != null)
            {
                foreach (Storage.Recipient recipient in msg.Recipients)
                {
                    var recipientEmail = recipient.Email ?? recipient.DisplayName ?? string.Empty;

                    try
                    {
                        var typeString = recipient.Type?.ToString() ?? string.Empty;

                        if (typeString.Contains("To", StringComparison.OrdinalIgnoreCase))
                            toRecipients.Add(recipientEmail);
                        else if (typeString.Contains("Cc", StringComparison.OrdinalIgnoreCase))
                            ccRecipients.Add(recipientEmail);
                        else
                            toRecipients.Add(recipientEmail);
                    }
                    catch
                    {
                        toRecipients.Add(recipientEmail);
                    }
                }
            }

            string htmlBody = string.Empty;
            string textBody = string.Empty;

            try { htmlBody = msg.BodyHtml ?? string.Empty; }
            catch (Exception ex) { System.Console.WriteLine($"  Warning: Could not extract HTML body: {ex.Message}"); }

            try { textBody = msg.BodyText ?? string.Empty; }
            catch (Exception ex) { System.Console.WriteLine($"  Warning: Could not extract text body: {ex.Message}"); }

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

            if (msg.Attachments != null && msg.Attachments.Count > 0)
            {
                foreach (var attachment in msg.Attachments)
                {
                    if (attachment is Storage.Attachment att && att.Data != null)
                    {
                        var contentId = att.ContentId;
                        var fileName = att.FileName ?? $"Unnamed_Attachment_{emailMessage.Attachments.Count + 1}";

                        bool isEmbeddedImage = false;

                        if (!string.IsNullOrWhiteSpace(contentId))
                        {
                            var cleanContentId = contentId.Trim('<', '>');

                            if (!string.IsNullOrEmpty(htmlBody) &&
                                htmlBody.Contains($"cid:{cleanContentId}", StringComparison.OrdinalIgnoreCase))
                            {
                                isEmbeddedImage = true;
                            }
                        }

                        if (isEmbeddedImage)
                        {
                            var cleanContentId = contentId!.Trim('<', '>');
                            emailMessage.EmbeddedImages[cleanContentId] = att.Data;
                        }
                        else
                        {
                            emailMessage.Attachments.Add(fileName);
                            emailMessage.AttachmentData[fileName] = att.Data;
                        }
                    }
                }
            }

            return emailMessage;
        });
    }

    public bool CanParse(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".msg", StringComparison.OrdinalIgnoreCase);
    }

    private EmailType DetectEmailType(Storage.Message msg)
    {
        var htmlBody = msg.BodyHtml ?? string.Empty;
        var headerText = msg.Headers?.ToString() ?? string.Empty;

        // Check headers first
        if (!string.IsNullOrEmpty(headerText))
        {
            if (headerText.Contains("X-Google", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("X-Gm-Message-State", StringComparison.OrdinalIgnoreCase))
                return EmailType.Gmail;

            if (headerText.Contains("Microsoft.Exchange.Transport", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("X-MS-Exchange", StringComparison.OrdinalIgnoreCase))
                return EmailType.Office365;

            if (headerText.Contains("X-Pm-", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("protonmail", StringComparison.OrdinalIgnoreCase))
                return EmailType.ProtonMail;

            if (headerText.Contains("Thunderbird", StringComparison.OrdinalIgnoreCase))
                return EmailType.Thunderbird;

            if (headerText.Contains("YMailISG", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("X-Yahoo", StringComparison.OrdinalIgnoreCase))
                return EmailType.YahooMail;

            if (headerText.Contains("X-Zoho", StringComparison.OrdinalIgnoreCase))
                return EmailType.ZohoMail;

            if (headerText.Contains("X-Mailer", StringComparison.OrdinalIgnoreCase))
            {
                if (headerText.Contains("Apple", StringComparison.OrdinalIgnoreCase))
                    return EmailType.Apple;
                if (headerText.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                    return EmailType.Outlook;
            }
        }

        // Check HTML body patterns
        if (!string.IsNullOrEmpty(htmlBody))
        {
            if (htmlBody.Contains("gmail_quote", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("gmail_signature", StringComparison.OrdinalIgnoreCase))
                return EmailType.Gmail;

            if (htmlBody.Contains("Apple-interchange-newline", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("AppleMailSignature", StringComparison.OrdinalIgnoreCase))
                return EmailType.Apple;

            if (htmlBody.Contains("MsoNormal", StringComparison.OrdinalIgnoreCase) ||
                htmlBody.Contains("WordSection", StringComparison.OrdinalIgnoreCase))
            {
                if (htmlBody.Contains("safelink.protection.outlook.com", StringComparison.OrdinalIgnoreCase))
                    return EmailType.Office365;
                return EmailType.Outlook;
            }

            if (htmlBody.Contains("OWALink", StringComparison.OrdinalIgnoreCase))
                return EmailType.OutlookWeb;

            if (htmlBody.Contains("moz-signature", StringComparison.OrdinalIgnoreCase))
                return EmailType.Thunderbird;

            if (htmlBody.Contains("yahoo-style-wrap", StringComparison.OrdinalIgnoreCase))
                return EmailType.YahooMail;

            if (htmlBody.Contains("protonmail_quote", StringComparison.OrdinalIgnoreCase))
                return EmailType.ProtonMail;

            if (htmlBody.Contains("zmail_", StringComparison.OrdinalIgnoreCase))
                return EmailType.ZohoMail;
        }

        return EmailType.Outlook;
    }
}
