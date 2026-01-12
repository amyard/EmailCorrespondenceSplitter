using System.Text.RegularExpressions;
using EmailCorrespondenceSplitter.Models;
using HtmlAgilityPack;

namespace EmailCorrespondenceSplitter.Services;

/// <summary>
/// Detects and extracts individual correspondences from email threads
/// </summary>
public class CorrespondenceDetector
{
    /// <summary>
    /// Extract individual correspondences from an email message
    /// </summary>
    public List<Correspondence> DetectCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        
        if (string.IsNullOrWhiteSpace(email.HtmlBody))
        {
            // If no HTML body, treat the entire email as single correspondence
            correspondences.Add(CreateSingleCorrespondence(email));
            return correspondences;
        }
        
        // Detect correspondences based on email type
        correspondences = email.EmailType switch
        {
            EmailType.Gmail => DetectGmailCorrespondences(email),
            EmailType.Outlook => DetectOutlookCorrespondences(email),
            EmailType.Apple => DetectAppleCorrespondences(email),
            _ => DetectGenericCorrespondences(email)
        };
        
        // If no correspondences detected, treat as single email
        if (correspondences.Count == 0)
        {
            correspondences.Add(CreateSingleCorrespondence(email));
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Detect Gmail correspondences (uses gmail_quote divs)
    /// </summary>
    private List<Correspondence> DetectGmailCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // Gmail typically uses <div class="gmail_quote"> to separate quoted content
        var quoteBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class, 'gmail_quote')]");
        
        if (quoteBlocks != null && quoteBlocks.Count > 0)
        {
            // First correspondence is everything before the first quote
            var mainContent = ExtractContentBeforeNode(doc.DocumentNode, quoteBlocks[0]);
            
            correspondences.Add(new Correspondence
            {
                From = email.From,
                To = email.To,
                SentOn = email.SentOn,
                Subject = email.Subject,
                HtmlContent = mainContent,
                TextContent = HtmlToPlainText(mainContent),
                Index = 0,
                IsParent = true
            });
            
            // Process quoted sections
            int index = 1;
            foreach (var quoteBlock in quoteBlocks)
            {
                var quotedContent = quoteBlock.InnerHtml;
                var metadata = ExtractEmailMetadata(quotedContent);
                
                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? "Unknown",
                    To = metadata.To ?? email.From, // Assuming reply chain
                    SentOn = metadata.Date,
                    Subject = email.Subject,
                    HtmlContent = quotedContent,
                    TextContent = HtmlToPlainText(quotedContent),
                    Index = index++,
                    IsParent = false
                });
            }
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Detect Outlook correspondences (uses horizontal lines and specific formatting)
    /// </summary>
    private List<Correspondence> DetectOutlookCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // Outlook uses <hr> or specific divs to separate emails
        var separators = doc.DocumentNode.SelectNodes("//hr | //div[contains(@style, 'border-top')]");
        
        if (separators != null && separators.Count > 0)
        {
            // First correspondence
            var mainContent = ExtractContentBeforeNode(doc.DocumentNode, separators[0]);
            
            correspondences.Add(new Correspondence
            {
                From = email.From,
                To = email.To,
                SentOn = email.SentOn,
                Subject = email.Subject,
                HtmlContent = mainContent,
                TextContent = HtmlToPlainText(mainContent),
                Index = 0,
                IsParent = true
            });
            
            // Extract content between separators
            for (int i = 0; i < separators.Count; i++)
            {
                var nextNode = separators[i].NextSibling;
                var quotedContent = ExtractContentAfterNode(separators[i]);
                
                if (!string.IsNullOrWhiteSpace(quotedContent))
                {
                    var metadata = ExtractEmailMetadata(quotedContent);
                    
                    correspondences.Add(new Correspondence
                    {
                        From = metadata.From ?? "Unknown",
                        To = metadata.To ?? email.From,
                        SentOn = metadata.Date,
                        Subject = email.Subject,
                        HtmlContent = quotedContent,
                        TextContent = HtmlToPlainText(quotedContent),
                        Index = i + 1,
                        IsParent = false
                    });
                }
            }
        }
        else
        {
            // Look for "From:" pattern which is common in Outlook forwarded emails
            var fromPattern = @"<b>From:</b>|<strong>From:</strong>|From:";
            var matches = Regex.Matches(email.HtmlBody, fromPattern, RegexOptions.IgnoreCase);
            
            if (matches.Count > 0)
            {
                correspondences = SplitByFromPattern(email);
            }
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Detect Apple Mail correspondences
    /// </summary>
    private List<Correspondence> DetectAppleCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // Apple Mail uses blockquote for quoted content
        var blockquotes = doc.DocumentNode.SelectNodes("//blockquote[@type='cite']");
        
        if (blockquotes != null && blockquotes.Count > 0)
        {
            // First correspondence
            var mainContent = ExtractContentBeforeNode(doc.DocumentNode, blockquotes[0]);
            
            correspondences.Add(new Correspondence
            {
                From = email.From,
                To = email.To,
                SentOn = email.SentOn,
                Subject = email.Subject,
                HtmlContent = mainContent,
                TextContent = HtmlToPlainText(mainContent),
                Index = 0,
                IsParent = true
            });
            
            // Process blockquotes
            int index = 1;
            foreach (var blockquote in blockquotes)
            {
                var quotedContent = blockquote.InnerHtml;
                var metadata = ExtractEmailMetadata(quotedContent);
                
                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? "Unknown",
                    To = metadata.To ?? email.From,
                    SentOn = metadata.Date,
                    Subject = email.Subject,
                    HtmlContent = quotedContent,
                    TextContent = HtmlToPlainText(quotedContent),
                    Index = index++,
                    IsParent = false
                });
            }
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Generic correspondence detection using common patterns
    /// </summary>
    private List<Correspondence> DetectGenericCorrespondences(EmailMessage email)
    {
        // Try to split by common patterns
        var fromPattern = @"(?:From:|Sent:|To:|Subject:)";
        var matches = Regex.Matches(email.HtmlBody, fromPattern, RegexOptions.IgnoreCase);
        
        if (matches.Count > 1)
        {
            return SplitByFromPattern(email);
        }
        
        return new List<Correspondence>();
    }
    
    /// <summary>
    /// Split email by "From:" pattern (common in forwarded emails)
    /// </summary>
    private List<Correspondence> SplitByFromPattern(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        
        // Pattern to match email headers in forwarded messages
        var headerPattern = @"(?:From:|Sent:|To:|Subject:).*?(?=From:|Sent:|$)";
        var sections = Regex.Split(email.HtmlBody, @"(?=<b>From:</b>|<strong>From:</strong>|From:)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        for (int i = 0; i < sections.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(sections[i]))
            {
                var metadata = ExtractEmailMetadata(sections[i]);
                
                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? (i == 0 ? email.From : "Unknown"),
                    To = metadata.To ?? (i == 0 ? email.To : email.From),
                    SentOn = metadata.Date ?? (i == 0 ? email.SentOn : null),
                    Subject = email.Subject,
                    HtmlContent = sections[i],
                    TextContent = HtmlToPlainText(sections[i]),
                    Index = i,
                    IsParent = i == 0
                });
            }
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Extract email metadata (From, To, Date) from HTML content
    /// </summary>
    private (string? From, string? To, DateTime? Date) ExtractEmailMetadata(string htmlContent)
    {
        string? from = null;
        string? to = null;
        DateTime? date = null;
        
        // Remove HTML tags for easier parsing
        var text = HtmlToPlainText(htmlContent);
        
        // Extract From
        var fromMatch = Regex.Match(text, @"From:\s*(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase);
        if (fromMatch.Success)
        {
            from = fromMatch.Groups[1].Value.Trim();
        }
        
        // Extract To
        var toMatch = Regex.Match(text, @"To:\s*(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase);
        if (toMatch.Success)
        {
            to = toMatch.Groups[1].Value.Trim();
        }
        
        // Extract Date/Sent
        var dateMatch = Regex.Match(text, @"(?:Sent|Date):\s*(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase);
        if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var parsedDate))
        {
            date = parsedDate;
        }
        
        return (from, to, date);
    }
    
    /// <summary>
    /// Extract HTML content before a specific node
    /// </summary>
    private string ExtractContentBeforeNode(HtmlNode rootNode, HtmlNode beforeNode)
    {
        var content = new System.Text.StringBuilder();
        
        foreach (var node in rootNode.ChildNodes)
        {
            if (node == beforeNode)
                break;
                
            content.Append(node.OuterHtml);
        }
        
        return content.ToString();
    }
    
    /// <summary>
    /// Extract HTML content after a specific node
    /// </summary>
    private string ExtractContentAfterNode(HtmlNode afterNode)
    {
        var content = new System.Text.StringBuilder();
        var currentNode = afterNode.NextSibling;
        
        while (currentNode != null)
        {
            // Stop at next separator
            if (currentNode.Name == "hr" || 
                (currentNode.Attributes["style"]?.Value?.Contains("border-top") == true))
                break;
                
            content.Append(currentNode.OuterHtml);
            currentNode = currentNode.NextSibling;
        }
        
        return content.ToString();
    }
    
    /// <summary>
    /// Create a single correspondence from the entire email
    /// </summary>
    private Correspondence CreateSingleCorrespondence(EmailMessage email)
    {
        return new Correspondence
        {
            From = email.From,
            To = email.To,
            SentOn = email.SentOn,
            Subject = email.Subject,
            HtmlContent = email.HtmlBody,
            TextContent = email.TextBody,
            Index = 0,
            IsParent = true
        };
    }
    
    /// <summary>
    /// Convert HTML to plain text
    /// </summary>
    private string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;
            
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        return doc.DocumentNode.InnerText;
    }
}
