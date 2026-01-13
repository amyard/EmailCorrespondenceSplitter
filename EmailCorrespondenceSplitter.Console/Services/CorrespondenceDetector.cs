using System.Text.RegularExpressions;
using EmailCorrespondenceSplitter.Models;
using HtmlAgilityPack;

namespace EmailCorrespondenceSplitter.Services;

/// <summary>
/// Detects and extracts individual correspondences from email threads
/// Enhanced to support multiple email client types
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
            EmailType.Office365 => DetectOffice365Correspondences(email),
            EmailType.OutlookWeb => DetectOutlookWebCorrespondences(email),
            EmailType.Apple => DetectAppleCorrespondences(email),
            EmailType.Thunderbird => DetectThunderbirdCorrespondences(email),
            EmailType.YahooMail => DetectYahooMailCorrespondences(email),
            EmailType.ProtonMail => DetectProtonMailCorrespondences(email),
            EmailType.ZohaMail => DetectZohoMailCorrespondences(email),
            EmailType.Generic => DetectGenericCorrespondences(email),
            _ => DetectUniversalCorrespondences(email) // Ultimate fallback with all patterns
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
                IsParent = true,
                Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
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
    private List<Correspondence> DetectOutlookCorrespondences(EmailMessage email, bool skipOwaCheck = false)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // Check if this is actually OWA format (HR followed by divRplyFwdMsg)
        if (!skipOwaCheck)
        {
            var divRplyFwdMsgs = doc.DocumentNode.SelectNodes("//div[@id='divRplyFwdMsg']");
            if (divRplyFwdMsgs != null && divRplyFwdMsgs.Count > 0)
            {
                // This is OWA format, use OWA detection
                return DetectOutlookWebCorrespondences(email);
            }
        }
        
        // Outlook uses <hr> or specific divs to separate emails
        // Important: Only select top-level HR tags, not nested ones within quoted content
        // Nested HRs (inside divRplyFwdMsg, mail-editor-reference-message-container) should not split correspondences
        var allHrs = doc.DocumentNode.SelectNodes("//hr");
        
        // Filter out nested HRs that are inside quoted message containers
        List<HtmlNode>? separators = null;
        if (allHrs != null)
        {
            separators = new List<HtmlNode>();
            foreach (var hr in allHrs)
            {
                // Check if this HR is inside a quoted message container
                var parent = hr.ParentNode;
                bool isNestedInQuote = false;
                
                while (parent != null)
                {
                    // Skip HRs that are inside divRplyFwdMsg, mail-editor-reference-message-container,
                    // or any container with these IDs in the hierarchy
                    // These are part of the quoted content, not correspondence separators
                    var parentId = parent.GetAttributeValue("id", "");
                    var parentClass = parent.GetAttributeValue("class", "");
                    
                    if (parentId == "divRplyFwdMsg" || 
                        parentId == "mail-editor-reference-message-container" ||
                        parentId == "appendonsend" ||
                        parentClass.Contains("gmail_quote") ||
                        parent.Name == "blockquote")
                    {
                        isNestedInQuote = true;
                        break;
                    }
                    parent = parent.ParentNode;
                }
                
                if (!isNestedInQuote)
                {
                    separators.Add(hr);
                }
            }
        }
        
        if (separators != null && separators.Count > 0)
        {
            // Extract content before first separator as main correspondence
            var firstSeparator = separators[0];
            var mainContent = ExtractAllContentBeforeSeparator(doc, firstSeparator);
            
            if (!string.IsNullOrWhiteSpace(mainContent))
            {
                // Extract images referenced in this correspondence's HTML
                var imagesForCorrespondence = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages);
                
                correspondences.Add(new Correspondence
                {
                    From = email.From,
                    To = email.To,
                    SentOn = email.SentOn,
                    Subject = email.Subject,
                    HtmlContent = mainContent,
                    TextContent = HtmlToPlainText(mainContent),
                    Index = 0,
                    IsParent = true,
                    EmbeddedImages = imagesForCorrespondence,
                    Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
                });
            }
            
            // Extract content between and after separators
            for (int i = 0; i < separators.Count; i++)
            {
                HtmlNode? nextSeparator = i < separators.Count - 1 ? separators[i + 1] : null;
                var quotedContent = ExtractContentBetweenNodes(separators[i], nextSeparator);
                
                if (!string.IsNullOrWhiteSpace(quotedContent))
                {
                    var metadata = ExtractEmailMetadata(quotedContent);
                    var imagesForCorrespondence = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages);
                    
                    correspondences.Add(new Correspondence
                    {
                        From = metadata.From ?? "Unknown",
                        To = metadata.To ?? email.From,
                        SentOn = metadata.Date,
                        Subject = email.Subject,
                        HtmlContent = quotedContent,
                        TextContent = HtmlToPlainText(quotedContent),
                        Index = correspondences.Count,
                        IsParent = false,
                        EmbeddedImages = imagesForCorrespondence
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
    /// Extract images that are referenced in the given HTML content
    /// </summary>
    /// <param name="htmlContent">HTML content to search for image references</param>
    /// <param name="allEmbeddedImages">All embedded images from the email</param>
    /// <returns>Dictionary of images referenced in this HTML</returns>
    private Dictionary<string, byte[]> ExtractImagesForHtmlContent(string htmlContent, Dictionary<string, byte[]> allEmbeddedImages)
    {
        var imagesForContent = new Dictionary<string, byte[]>();
        
        if (string.IsNullOrWhiteSpace(htmlContent) || allEmbeddedImages.Count == 0)
        {
            return imagesForContent;
        }
        
        // Find all cid: references in the HTML
        // Pattern: src="cid:xxx" or src='cid:xxx' or background="cid:xxx"
        var cidPattern = @"(?:src|background)\s*=\s*['""]cid:([^'""]+)['""]";
        var matches = Regex.Matches(htmlContent, cidPattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            var contentId = match.Groups[1].Value;
            
            // Try to find the image in the embedded images collection
            if (allEmbeddedImages.TryGetValue(contentId, out var imageData))
            {
                imagesForContent[contentId] = imageData;
            }
            else
            {
                Console.WriteLine($"  Warning: Image reference cid:{contentId} found but image data not available");
            }
        }
        
        return imagesForContent;
    }
    
    /// <summary>
    /// Extract all content before a separator node by walking up the tree
    /// </summary>
    private string ExtractAllContentBeforeSeparator(HtmlDocument doc, HtmlNode separator)
    {
        var content = new System.Text.StringBuilder();
        
        // Walk through all top-level nodes until we find the one containing the separator
        foreach (var topNode in doc.DocumentNode.ChildNodes)
        {
            if (ContainsNode(topNode, separator))
            {
                // Extract content from this node up to the separator
                ExtractBeforeNodeInSubtree(topNode, separator, content);
                break;
            }
            else
            {
                // This entire node is before the separator
                content.Append(topNode.OuterHtml);
            }
        }
        
        return content.ToString();
    }
    
    /// <summary>
    /// Check if a node contains another node in its subtree
    /// </summary>
    private bool ContainsNode(HtmlNode parent, HtmlNode target)
    {
        if (parent == target) return true;
        
        foreach (var child in parent.ChildNodes)
        {
            if (ContainsNode(child, target))
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Extract content from a subtree before a target node
    /// </summary>
    private void ExtractBeforeNodeInSubtree(HtmlNode node, HtmlNode target, System.Text.StringBuilder content)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child == target)
            {
                // Found the target, stop here
                return;
            }
            
            if (ContainsNode(child, target))
            {
                // Target is in this child's subtree, recurse
                ExtractBeforeNodeInSubtree(child, target, content);
                return;
            }
            else
            {
                // This child is completely before the target
                content.Append(child.OuterHtml);
            }
        }
    }
    
    /// <summary>
    /// Detect Office 365 correspondences
    /// </summary>
    private List<Correspondence> DetectOffice365Correspondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // Office 365 uses similar patterns to Outlook but with additional metadata
        // Try multiple patterns
        
        // Pattern 1: HR separators
        var separators = doc.DocumentNode.SelectNodes("//hr");
        if (separators != null && separators.Count > 0)
        {
            return ExtractWithSeparators(email, doc, separators);
        }
        
        // Pattern 2: Original message divider
        var originalMessageDivs = doc.DocumentNode.SelectNodes("//div[contains(., 'Original Message') or contains(., 'Original Appointment')]");
        if (originalMessageDivs != null && originalMessageDivs.Count > 0)
        {
            return ExtractWithOriginalMessageMarkers(email, doc, originalMessageDivs);
        }
        
        // Pattern 3: From/Sent/To header blocks
        return DetectOutlookCorrespondences(email);
    }
    
    /// <summary>
    /// Detect Outlook Web App (OWA) correspondences
    /// </summary>
    private List<Correspondence> DetectOutlookWebCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // OWA uses specific div structures, but in MSG files saved from Outlook,
        // the divRplyFwdMsg might be mixed with regular Outlook HR separators
        // Check for HR separators first
        var separators = doc.DocumentNode.SelectNodes("//hr");
        
        if (separators != null && separators.Count > 0)
        {
            // This is a mixed format - has both OWA divs and HR separators
            // Use the HR-based extraction which handles the structure properly
            // BUT: divRplyFwdMsg sections should NOT be treated as separate correspondences
            // They are email headers that are part of the correspondence content
            return DetectOutlookCorrespondences(email, skipOwaCheck: true);
        }
        
        // Pure OWA format without HR separators - only process if divRplyFwdMsg is theONLY quoted structure
        // If there are HR tags, those take precedence for splitting
        var quoteDivs = doc.DocumentNode.SelectNodes("//div[@id='divRplyFwdMsg' or @id='appendonsend' or contains(@class, 'BodyFragment')]");
        
        if (quoteDivs != null && quoteDivs.Count > 0)
        {
            // Extract main content (everything before first quoted div)
            var firstQuoteDiv = quoteDivs[0];
            var mainContent = ExtractAllContentBeforeSeparator(doc, firstQuoteDiv);
            
            if (!string.IsNullOrWhiteSpace(mainContent))
            {
                correspondences.Add(new Correspondence
                {
                    From = email.From,
                    To = email.To,
                    SentOn = email.SentOn,
                    Subject = email.Subject,
                    HtmlContent = mainContent,
                    TextContent = HtmlToPlainText(mainContent),
                    Index = 0,
                    IsParent = true,
                    Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
                });
            }
            
            // Process each quoted div as a separate correspondence
            // NOTE: In the presence of HR tags, this code path should not be reached
            for (int i = 0; i < quoteDivs.Count; i++)
            {
                var quotedContent = quoteDivs[i].InnerHtml;
                
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
                        Index = correspondences.Count,
                        IsParent = false
                    });
                }
            }
        }
        else
        {
            // Fallback to Outlook detection (with skip flag to prevent recursion)
            return DetectOutlookCorrespondences(email, skipOwaCheck: true);
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Detect Thunderbird correspondences
    /// </summary>
    private List<Correspondence> DetectThunderbirdCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // Thunderbird uses moz-cite-prefix or blockquote type="cite"
        var citeBlocks = doc.DocumentNode.SelectNodes("//blockquote[@type='cite'] | //div[contains(@class, 'moz-cite-prefix')]");
        
        if (citeBlocks != null && citeBlocks.Count > 0)
        {
            // First correspondence
            var mainContent = ExtractContentBeforeNode(doc.DocumentNode, citeBlocks[0]);
            
            correspondences.Add(new Correspondence
            {
                From = email.From,
                To = email.To,
                SentOn = email.SentOn,
                Subject = email.Subject,
                HtmlContent = mainContent,
                TextContent = HtmlToPlainText(mainContent),
                Index = 0,
                IsParent = true,
                Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
            });
            
            // Process quoted sections
            int index = 1;
            foreach (var citeBlock in citeBlocks)
            {
                var quotedContent = citeBlock.InnerHtml;
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
        else
        {
            // Fallback to generic detection
            return DetectGenericCorrespondences(email);
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Detect Yahoo Mail correspondences
    /// </summary>
    private List<Correspondence> DetectYahooMailCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // Yahoo uses yahoo-style-wrap divs or yiv prefixed classes
        var quoteBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class, 'yahoo-style-wrap')] | //div[contains(@class, 'qtdSeparateBR')] | //blockquote");
        
        if (quoteBlocks != null && quoteBlocks.Count > 0)
        {
            // First correspondence
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
                IsParent = true,
                Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
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
        else
        {
            return DetectGenericCorrespondences(email);
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Detect ProtonMail correspondences
    /// </summary>
    private List<Correspondence> DetectProtonMailCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // ProtonMail uses protonmail_quote class and standard blockquotes
        var quoteBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class, 'protonmail_quote')] | //blockquote[contains(@class, 'protonmail')] | //blockquote");
        
        if (quoteBlocks != null && quoteBlocks.Count > 0)
        {
            return ExtractWithQuoteBlocks(email, quoteBlocks);
        }
        
        return DetectGenericCorrespondences(email);
    }
    
    /// <summary>
    /// Detect Zoho Mail correspondences
    /// </summary>
    private List<Correspondence> DetectZohoMailCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // Zoho uses specific div structures
        var quoteBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class, 'zmail_')] | //blockquote | //div[contains(@id, 'Zm')]");
        
        if (quoteBlocks != null && quoteBlocks.Count > 0)
        {
            return ExtractWithQuoteBlocks(email, quoteBlocks);
        }
        
        return DetectGenericCorrespondences(email);
    }
    
    /// <summary>
    /// Universal correspondence detection - tries all known patterns
    /// This is the ultimate fallback that combines all detection strategies
    /// </summary>
    private List<Correspondence> DetectUniversalCorrespondences(EmailMessage email)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);
        
        // Try multiple detection strategies in order of reliability
        
        // 1. Try blockquote elements (most universal)
        var blockquotes = doc.DocumentNode.SelectNodes("//blockquote");
        if (blockquotes != null && blockquotes.Count > 0)
        {
            var result = ExtractWithQuoteBlocks(email, blockquotes);
            if (result.Count > 0) return result;
        }
        
        // 2. Try HR separators
        var hrs = doc.DocumentNode.SelectNodes("//hr");
        if (hrs != null && hrs.Count > 0)
        {
            var result = ExtractWithSeparators(email, doc, hrs);
            if (result.Count > 0) return result;
        }
        
        // 3. Try quote divs (various patterns)
        var quoteDivs = doc.DocumentNode.SelectNodes("//div[contains(@class, 'quote') or contains(@class, 'quoted') or contains(@id, 'quote')]");
        if (quoteDivs != null && quoteDivs.Count > 0)
        {
            var result = ExtractWithQuoteBlocks(email, quoteDivs);
            if (result.Count > 0) return result;
        }
        
        // 4. Try From: pattern matching
        return DetectGenericCorrespondences(email);
    }
    
    /// <summary>
    /// Helper: Extract correspondences using separator nodes (HR, dividers)
    /// </summary>
    private List<Correspondence> ExtractWithSeparators(EmailMessage email, HtmlDocument doc, HtmlNodeCollection separators)
    {
        var correspondences = new List<Correspondence>();
        
        // First correspondence - content before first separator
        var mainContent = ExtractContentBeforeNode(doc.DocumentNode, separators[0]);
        var imagesForMain = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages);
        
        correspondences.Add(new Correspondence
        {
            From = email.From,
            To = email.To,
            SentOn = email.SentOn,
            Subject = email.Subject,
            HtmlContent = mainContent,
            TextContent = HtmlToPlainText(mainContent),
            Index = 0,
            IsParent = true,
            EmbeddedImages = imagesForMain,
            Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
        });
        
        // Extract content between and after separators
        for (int i = 0; i < separators.Count; i++)
        {
            HtmlNode? nextSeparator = i < separators.Count - 1 ? separators[i + 1] : null;
            var quotedContent = ExtractContentBetweenNodes(separators[i], nextSeparator);
            
            if (!string.IsNullOrWhiteSpace(quotedContent))
            {
                var metadata = ExtractEmailMetadata(quotedContent);
                var imagesForCorrespondence = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages);
                
                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? "Unknown",
                    To = metadata.To ?? email.From,
                    SentOn = metadata.Date,
                    Subject = email.Subject,
                    HtmlContent = quotedContent,
                    TextContent = HtmlToPlainText(quotedContent),
                    Index = i + 1,
                    IsParent = false,
                    EmbeddedImages = imagesForCorrespondence
                });
            }
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Helper: Extract correspondences using quote blocks (blockquote, div.quote, etc.)
    /// </summary>
    private List<Correspondence> ExtractWithQuoteBlocks(EmailMessage email, HtmlNodeCollection quoteBlocks)
    {
        var correspondences = new List<Correspondence>();
        
        // First correspondence
        var doc = quoteBlocks[0].OwnerDocument;
        var mainContent = ExtractContentBeforeNode(doc.DocumentNode, quoteBlocks[0]);
        var imagesForMain = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages);
        
        correspondences.Add(new Correspondence
        {
            From = email.From,
            To = email.To,
            SentOn = email.SentOn,
            Subject = email.Subject,
            HtmlContent = mainContent,
            TextContent = HtmlToPlainText(mainContent),
            Index = 0,
            IsParent = true,
            EmbeddedImages = imagesForMain,
            Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
        });
        
        // Process quoted sections
        int index = 1;
        foreach (var quoteBlock in quoteBlocks)
        {
            var quotedContent = quoteBlock.InnerHtml;
            var metadata = ExtractEmailMetadata(quotedContent);
            var imagesForCorrespondence = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages);
            
            correspondences.Add(new Correspondence
            {
                From = metadata.From ?? "Unknown",
                To = metadata.To ?? email.From,
                SentOn = metadata.Date,
                Subject = email.Subject,
                HtmlContent = quotedContent,
                TextContent = HtmlToPlainText(quotedContent),
                Index = index++,
                IsParent = false,
                EmbeddedImages = imagesForCorrespondence
            });
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Helper: Extract correspondences using "Original Message" markers
    /// </summary>
    private List<Correspondence> ExtractWithOriginalMessageMarkers(EmailMessage email, HtmlDocument doc, HtmlNodeCollection markers)
    {
        var correspondences = new List<Correspondence>();
        
        // First correspondence
        var mainContent = ExtractContentBeforeNode(doc.DocumentNode, markers[0]);
        var imagesForMain = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages);
        
        correspondences.Add(new Correspondence
        {
            From = email.From,
            To = email.To,
            SentOn = email.SentOn,
            Subject = email.Subject,
            HtmlContent = mainContent,
            TextContent = HtmlToPlainText(mainContent),
            Index = 0,
            IsParent = true,
            EmbeddedImages = imagesForMain,
            Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
        });
        
        // Extract content after each marker
        int index = 1;
        foreach (var marker in markers)
        {
            var quotedContent = ExtractContentAfterNode(marker);
            
            if (!string.IsNullOrWhiteSpace(quotedContent))
            {
                var metadata = ExtractEmailMetadata(quotedContent);
                var imagesForCorrespondence = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages);
                
                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? "Unknown",
                    To = metadata.To ?? email.From,
                    SentOn = metadata.Date,
                    Subject = email.Subject,
                    HtmlContent = quotedContent,
                    TextContent = HtmlToPlainText(quotedContent),
                    Index = index++,
                    IsParent = false,
                    EmbeddedImages = imagesForCorrespondence
                });
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
        // Note: Blockquotes can be nested, so we need to process them carefully
        var allBlockquotes = doc.DocumentNode.SelectNodes("//blockquote[@type='cite']");
        
        if (allBlockquotes != null && allBlockquotes.Count > 0)
        {
            // Get only top-level blockquotes (not nested within other blockquotes)
            var topLevelBlockquotes = new List<HtmlNode>();
            foreach (var blockquote in allBlockquotes)
            {
                // Check if this blockquote is nested inside another blockquote
                bool isNested = false;
                var parent = blockquote.ParentNode;
                while (parent != null)
                {
                    if (parent.Name == "blockquote" && parent.GetAttributeValue("type", "") == "cite")
                    {
                        isNested = true;
                        break;
                    }
                    parent = parent.ParentNode;
                }
                
                if (!isNested)
                {
                    topLevelBlockquotes.Add(blockquote);
                }
            }
            
            if (topLevelBlockquotes.Count > 0)
            {
                // First correspondence - extract content before first blockquote
                // Use ExtractAllContentBeforeSeparator to properly handle nested structures
                var mainContentBeforeQuote = ExtractAllContentBeforeSeparator(doc, topLevelBlockquotes[0]);
                
                // In Apple Mail, the blockquote may contain footer/signature content from the parent
                // correspondence before the actual quote starts. The quote typically starts with
                // "On [date], at [time], [email] wrote:" pattern
                var firstBlockquoteContent = topLevelBlockquotes[0].InnerHtml;
                var prefixContent = ExtractAppleMailQuotePrefix(firstBlockquoteContent);
                
                // Combine main content with any prefix content from the blockquote
                var mainContent = mainContentBeforeQuote;
                if (!string.IsNullOrWhiteSpace(prefixContent))
                {
                    mainContent += prefixContent;
                }
                
                correspondences.Add(new Correspondence
                {
                    From = email.From,
                    To = email.To,
                    SentOn = email.SentOn,
                    Subject = email.Subject,
                    HtmlContent = mainContent,
                    TextContent = HtmlToPlainText(mainContent),
                    Index = 0,
                    IsParent = true,
                    Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
                });
                
                // Process each top-level blockquote - extract content before any nested blockquotes
                int index = 1;
                foreach (var blockquote in topLevelBlockquotes)
                {
                    // Find nested blockquotes within this blockquote
                    var nestedBlockquotes = blockquote.SelectNodes(".//blockquote[@type='cite']");
                    
                    // Remove the prefix content (belongs to parent) and get only the quoted part
                    var blockquoteHtml = blockquote.InnerHtml;
                    var quotedContent = RemoveAppleMailQuotePrefix(blockquoteHtml);
                    
                    // If there are nested blockquotes, extract only content before them
                    if (nestedBlockquotes != null && nestedBlockquotes.Count > 0)
                    {
                        var tempDoc = new HtmlDocument();
                        tempDoc.LoadHtml(quotedContent);
                        var firstNested = tempDoc.DocumentNode.SelectSingleNode("//blockquote[@type='cite']");
                        if (firstNested != null)
                        {
                            // Extract content before nested blockquote
                            var contentBeforeNested = ExtractAllContentBeforeSeparator(tempDoc, firstNested);
                            
                            // Also check if there's prefix content in the nested blockquote
                            var nestedPrefixContent = ExtractAppleMailQuotePrefix(firstNested.InnerHtml);
                            if (!string.IsNullOrWhiteSpace(nestedPrefixContent))
                            {
                                quotedContent = contentBeforeNested + nestedPrefixContent;
                            }
                            else
                            {
                                quotedContent = contentBeforeNested;
                              }
                        }
                    }
                    
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
        }
        
        return correspondences;
    }
    
    /// <summary>
    /// Extract the prefix content from an Apple Mail blockquote that belongs to the parent correspondence.
    /// Apple Mail includes content before the "On [date], at [time], [email] wrote:" line that is actually
    /// part of the previous correspondence (like footers, signatures).
    /// </summary>
    private string ExtractAppleMailQuotePrefix(string blockquoteContent)
    {
        // Pattern: "On [date], at [time], [email] wrote:"
        // This marks the beginning of the actual quoted content
        var quoteHeaderPattern = @"On\s+\d+\s+\w+\s+\d{4},\s+at\s+\d+:\d+,\s+.*?wrote:";
        var match = Regex.Match(blockquoteContent, quoteHeaderPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        if (match.Success)
        {
            // Everything before the quote header belongs to the parent correspondence
            return blockquoteContent.Substring(0, match.Index);
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// Remove the prefix content from an Apple Mail blockquote, leaving only the actual quoted email.
    /// </summary>
    private string RemoveAppleMailQuotePrefix(string blockquoteContent)
    {
        // Pattern: "On [date], at [time], [email] wrote:"
        var quoteHeaderPattern = @"On\s+\d+\s+\w+\s+\d{4},\s+at\s+\d+:\d+,\s+.*?wrote:";
        var match = Regex.Match(blockquoteContent, quoteHeaderPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        if (match.Success)
        {
            // Return everything from the quote header onwards
            return blockquoteContent.Substring(match.Index);
        }
        
        // If no quote header found, return the original content
        return blockquoteContent;
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
        var sections = Regex.Split(email.HtmlBody, @"(?=<b>From:</b>|<strong>From:</strong>|From:)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        for (int i = 0; i < sections.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(sections[i]))
            {
                var metadata = ExtractEmailMetadata(sections[i]);
                var imagesForCorrespondence = ExtractImagesForHtmlContent(sections[i], email.EmbeddedImages);
                
                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? (i == 0 ? email.From : "Unknown"),
                    To = metadata.To ?? (i == 0 ? email.To : email.From),
                    SentOn = metadata.Date ?? (i == 0 ? email.SentOn : null),
                    Subject = email.Subject,
                    HtmlContent = sections[i],
                    TextContent = HtmlToPlainText(sections[i]),
                    Index = i,
                    IsParent = i == 0,
                    EmbeddedImages = imagesForCorrespondence,
                    Attachments = i == 0 ? new Dictionary<string, byte[]>(email.AttachmentData) : new Dictionary<string, byte[]>()
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
    /// Extract HTML content between two nodes (or after a node if endNode is null)
    /// This handles the Outlook structure where HR tags are in their own divs
    /// </summary>
    /// <param name="startNode">The HR node after which to start extraction</param>
    /// <param name="endNode">The HR node before which to stop extraction (or null to go to the end)</param>
    /// <returns>HTML content between the two nodes</returns>
    private string ExtractContentBetweenNodes(HtmlNode startNode, HtmlNode? endNode)
    {
        var content = new System.Text.StringBuilder();
        
        // In Outlook emails, the HR is typically in its own div/container
        // We need to get the parent of the HR and then collect following siblings
        var startParent = startNode.ParentNode;
        if (startParent == null)
            return string.Empty;
        
        var endParent = endNode?.ParentNode;
        
        // Start from the sibling after the HR's parent container
        var currentNode = startParent.NextSibling;
        
        while (currentNode != null)
        {
            // Stop if we've reached the end marker's parent
            if (endParent != null && currentNode == endParent)
                break;
            
            // Skip pure whitespace text nodes
            if (currentNode.NodeType == HtmlAgilityPack.HtmlNodeType.Text && 
                string.IsNullOrWhiteSpace(currentNode.InnerText))
            {
                currentNode = currentNode.NextSibling;
                continue;
            }
            
            // Add this node's content
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
            IsParent = true,
            EmbeddedImages = new Dictionary<string, byte[]>(email.EmbeddedImages),
            Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
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
