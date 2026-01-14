using System.Text;
using System.Text.RegularExpressions;
using EmailCorrespondenceSplitter.Pdf.Console.Models;
using HtmlAgilityPack;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Detects and extracts individual correspondences from email threads.
/// Supports multiple email clients and languages.
/// </summary>
public class CorrespondenceDetector
{
    // Multi-language patterns for email header fields
    private static readonly string[] FromPatterns =
    [
        "From", "Von", "De", "Da", "??", "Od", "Från", "Fra", "???", "???", "?? ??", "???", "?????"
    ];

    private static readonly string[] SentPatterns =
    [
        "Sent", "Gesendet", "Envoyé", "Enviado", "Inviato", "??????????", "Wys?ano", "Skickat", "Sendt", "????", "????", "?? ??", "?????", "????"
    ];

    private static readonly string[] ToPatterns =
    [
        "To", "An", "À", "A", "????", "Do", "Till", "Til", "??", "???", "?? ??", "???", "??"
    ];

    private static readonly string[] SubjectPatterns =
    [
        "Subject", "Betreff", "Objet", "Asunto", "Oggetto", "????", "Temat", "Ämne", "Emne", "??", "??", "??", "???????", "????"
    ];

    private static readonly string[] CcPatterns =
    [
        "Cc", "CC", "Kopie", "Copie", "Copia", "?????", "Kopia", "??", "??", "??"
    ];

    public List<Correspondence> DetectCorrespondences(EmailMessage email)
    {
        if (string.IsNullOrWhiteSpace(email.HtmlBody))
        {
            return [CreateSingleCorrespondence(email)];
        }

        var correspondences = email.EmailType switch
        {
            EmailType.Gmail => DetectGmailCorrespondences(email),
            EmailType.Outlook => DetectOutlookCorrespondences(email),
            EmailType.Office365 => DetectOutlookCorrespondences(email),
            EmailType.OutlookWeb => DetectOutlookCorrespondences(email),
            EmailType.Apple => DetectAppleCorrespondences(email),
            EmailType.Thunderbird => DetectThunderbirdCorrespondences(email),
            EmailType.YahooMail => DetectGenericCorrespondences(email),
            EmailType.ProtonMail => DetectGenericCorrespondences(email),
            EmailType.ZohoMail => DetectGenericCorrespondences(email),
            _ => DetectUniversalCorrespondences(email)
        };

        if (correspondences.Count == 0)
        {
            correspondences.Add(CreateSingleCorrespondence(email));
        }

        return correspondences;
    }

    private List<Correspondence> DetectGmailCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);

        var quoteBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class, 'gmail_quote')]");

        if (quoteBlocks != null && quoteBlocks.Count > 0)
        {
            var mainContent = ExtractContentBeforeNode(doc.DocumentNode, quoteBlocks[0]);

            correspondences.Add(new Correspondence
            {
                From = email.From,
                To = email.To,
                Cc = email.Cc,
                SentOn = email.SentOn,
                Subject = email.Subject,
                HtmlContent = mainContent,
                TextContent = HtmlToPlainText(mainContent),
                Index = 0,
                IsParent = true,
                EmbeddedImages = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages),
                Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
            });

            int index = 1;
            foreach (var quoteBlock in quoteBlocks)
            {
                var quotedContent = quoteBlock.InnerHtml;
                var metadata = ExtractEmailMetadata(quotedContent);

                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? "Unknown",
                    To = metadata.To ?? email.From,
                    Cc = metadata.Cc ?? string.Empty,
                    SentOn = metadata.Date,
                    Subject = metadata.Subject ?? email.Subject,
                    HtmlContent = quotedContent,
                    TextContent = HtmlToPlainText(quotedContent),
                    Index = index++,
                    IsParent = false,
                    EmbeddedImages = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages)
                });
            }
        }

        return correspondences;
    }

    private List<Correspondence> DetectOutlookCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);

        // Check for divRplyFwdMsg (OWA format)
        var divRplyFwdMsgs = doc.DocumentNode.SelectNodes("//div[@id='divRplyFwdMsg']");
        if (divRplyFwdMsgs != null && divRplyFwdMsgs.Count > 0)
        {
            return ExtractWithDivMarkers(email, doc, divRplyFwdMsgs);
        }

        // Check for HR separators (filter out nested ones)
        var allHrs = doc.DocumentNode.SelectNodes("//hr");
        List<HtmlNode>? separators = null;

        if (allHrs != null)
        {
            separators = [];
            foreach (var hr in allHrs)
            {
                var parent = hr.ParentNode;
                bool isNestedInQuote = false;

                while (parent != null)
                {
                    var parentId = parent.GetAttributeValue("id", "");
                    var parentClass = parent.GetAttributeValue("class", "");

                    if (parentId == "divRplyFwdMsg" ||
                        parentId == "mail-editor-reference-message-container" ||
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
            var mainContent = ExtractAllContentBeforeSeparator(doc, separators[0]);

            if (!string.IsNullOrWhiteSpace(mainContent))
            {
                correspondences.Add(new Correspondence
                {
                    From = email.From,
                    To = email.To,
                    Cc = email.Cc,
                    SentOn = email.SentOn,
                    Subject = email.Subject,
                    HtmlContent = mainContent,
                    TextContent = HtmlToPlainText(mainContent),
                    Index = 0,
                    IsParent = true,
                    EmbeddedImages = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages),
                    Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
                });
            }

            for (int i = 0; i < separators.Count; i++)
            {
                HtmlNode? nextSeparator = i < separators.Count - 1 ? separators[i + 1] : null;
                var quotedContent = ExtractContentBetweenNodes(separators[i], nextSeparator);

                if (!string.IsNullOrWhiteSpace(quotedContent))
                {
                    var metadata = ExtractEmailMetadata(quotedContent);

                    correspondences.Add(new Correspondence
                    {
                        From = metadata.From ?? "Unknown",
                        To = metadata.To ?? email.From,
                        Cc = metadata.Cc ?? string.Empty,
                        SentOn = metadata.Date,
                        Subject = metadata.Subject ?? email.Subject,
                        HtmlContent = quotedContent,
                        TextContent = HtmlToPlainText(quotedContent),
                        Index = correspondences.Count,
                        IsParent = false,
                        EmbeddedImages = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages)
                    });
                }
            }

            return correspondences;
        }

        // Check for border-top divs
        var borderTopDivs = doc.DocumentNode.SelectNodes("//div[contains(@style, 'border-top')]");
        if (borderTopDivs != null && borderTopDivs.Count > 0)
        {
            var emailSeparators = borderTopDivs
                .Where(div =>
                {
                    var style = div.GetAttributeValue("style", "");
                    return style.Contains("border:none", StringComparison.OrdinalIgnoreCase) &&
                           style.Contains("border-top", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (emailSeparators.Count > 0)
            {
                return ExtractWithBorderDivs(email, doc, emailSeparators);
            }
        }

        // Fallback to From: pattern
        return SplitByFromPattern(email);
    }

    private List<Correspondence> DetectAppleCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);

        var allBlockquotes = doc.DocumentNode.SelectNodes("//blockquote[@type='cite']");

        if (allBlockquotes != null && allBlockquotes.Count > 0)
        {
            var topLevelBlockquotes = new List<HtmlNode>();
            foreach (var blockquote in allBlockquotes)
            {
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
                var mainContent = ExtractAllContentBeforeSeparator(doc, topLevelBlockquotes[0]);

                correspondences.Add(new Correspondence
                {
                    From = email.From,
                    To = email.To,
                    Cc = email.Cc,
                    SentOn = email.SentOn,
                    Subject = email.Subject,
                    HtmlContent = mainContent,
                    TextContent = HtmlToPlainText(mainContent),
                    Index = 0,
                    IsParent = true,
                    EmbeddedImages = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages),
                    Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
                });

                int index = 1;
                foreach (var blockquote in topLevelBlockquotes)
                {
                    var nestedBlockquotes = blockquote.SelectNodes(".//blockquote[@type='cite']");
                    var quotedContent = blockquote.InnerHtml;

                    if (nestedBlockquotes != null && nestedBlockquotes.Count > 0)
                    {
                        var tempDoc = new HtmlDocument();
                        tempDoc.LoadHtml(quotedContent);
                        var firstNested = tempDoc.DocumentNode.SelectSingleNode("//blockquote[@type='cite']");
                        if (firstNested != null)
                        {
                            quotedContent = ExtractAllContentBeforeSeparator(tempDoc, firstNested);
                        }
                    }

                    var metadata = ExtractEmailMetadata(quotedContent);

                    correspondences.Add(new Correspondence
                    {
                        From = metadata.From ?? "Unknown",
                        To = metadata.To ?? email.From,
                        Cc = metadata.Cc ?? string.Empty,
                        SentOn = metadata.Date,
                        Subject = metadata.Subject ?? email.Subject,
                        HtmlContent = quotedContent,
                        TextContent = HtmlToPlainText(quotedContent),
                        Index = index++,
                        IsParent = false,
                        EmbeddedImages = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages)
                    });
                }
            }
        }

        return correspondences;
    }

    private List<Correspondence> DetectThunderbirdCorrespondences(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);

        var citeBlocks = doc.DocumentNode.SelectNodes("//blockquote[@type='cite'] | //div[contains(@class, 'moz-cite-prefix')]");

        if (citeBlocks != null && citeBlocks.Count > 0)
        {
            var mainContent = ExtractContentBeforeNode(doc.DocumentNode, citeBlocks[0]);

            correspondences.Add(new Correspondence
            {
                From = email.From,
                To = email.To,
                Cc = email.Cc,
                SentOn = email.SentOn,
                Subject = email.Subject,
                HtmlContent = mainContent,
                TextContent = HtmlToPlainText(mainContent),
                Index = 0,
                IsParent = true,
                EmbeddedImages = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages),
                Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
            });

            int index = 1;
            foreach (var citeBlock in citeBlocks)
            {
                var quotedContent = citeBlock.InnerHtml;
                var metadata = ExtractEmailMetadata(quotedContent);

                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? "Unknown",
                    To = metadata.To ?? email.From,
                    Cc = metadata.Cc ?? string.Empty,
                    SentOn = metadata.Date,
                    Subject = metadata.Subject ?? email.Subject,
                    HtmlContent = quotedContent,
                    TextContent = HtmlToPlainText(quotedContent),
                    Index = index++,
                    IsParent = false,
                    EmbeddedImages = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages)
                });
            }
        }

        return correspondences;
    }

    private List<Correspondence> DetectGenericCorrespondences(EmailMessage email)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);

        var blockquotes = doc.DocumentNode.SelectNodes("//blockquote");
        if (blockquotes != null && blockquotes.Count > 0)
        {
            return ExtractWithQuoteBlocks(email, blockquotes);
        }

        return SplitByFromPattern(email);
    }

    private List<Correspondence> DetectUniversalCorrespondences(EmailMessage email)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(email.HtmlBody);

        // Try blockquotes
        var blockquotes = doc.DocumentNode.SelectNodes("//blockquote");
        if (blockquotes != null && blockquotes.Count > 0)
        {
            var result = ExtractWithQuoteBlocks(email, blockquotes);
            if (result.Count > 0) return result;
        }

        // Try HR separators
        var hrs = doc.DocumentNode.SelectNodes("//hr");
        if (hrs != null && hrs.Count > 0)
        {
            var result = ExtractWithHrSeparators(email, doc, hrs);
            if (result.Count > 0) return result;
        }

        // Try quote divs
        var quoteDivs = doc.DocumentNode.SelectNodes("//div[contains(@class, 'quote') or contains(@class, 'quoted')]");
        if (quoteDivs != null && quoteDivs.Count > 0)
        {
            var result = ExtractWithQuoteBlocks(email, quoteDivs);
            if (result.Count > 0) return result;
        }

        return SplitByFromPattern(email);
    }

    private List<Correspondence> ExtractWithDivMarkers(EmailMessage email, HtmlDocument doc, HtmlNodeCollection markers)
    {
        var correspondences = new List<Correspondence>();

        var mainContent = ExtractAllContentBeforeSeparator(doc, markers[0]);

        if (!string.IsNullOrWhiteSpace(mainContent))
        {
            correspondences.Add(new Correspondence
            {
                From = email.From,
                To = email.To,
                Cc = email.Cc,
                SentOn = email.SentOn,
                Subject = email.Subject,
                HtmlContent = mainContent,
                TextContent = HtmlToPlainText(mainContent),
                Index = 0,
                IsParent = true,
                EmbeddedImages = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages),
                Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
            });
        }

        for (int i = 0; i < markers.Count; i++)
        {
            var quotedContent = markers[i].InnerHtml;

            if (!string.IsNullOrWhiteSpace(quotedContent))
            {
                var metadata = ExtractEmailMetadata(quotedContent);

                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? "Unknown",
                    To = metadata.To ?? email.From,
                    Cc = metadata.Cc ?? string.Empty,
                    SentOn = metadata.Date,
                    Subject = metadata.Subject ?? email.Subject,
                    HtmlContent = quotedContent,
                    TextContent = HtmlToPlainText(quotedContent),
                    Index = correspondences.Count,
                    IsParent = false,
                    EmbeddedImages = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages)
                });
            }
        }

        return correspondences;
    }

    private List<Correspondence> ExtractWithBorderDivs(EmailMessage email, HtmlDocument doc, List<HtmlNode> separators)
    {
        var correspondences = new List<Correspondence>();

        // Extract content before first separator (parent email)
        var mainContent = ExtractAllContentBeforeSeparator(doc, separators[0]);

        correspondences.Add(new Correspondence
        {
            From = email.From,
            To = email.To,
            Cc = email.Cc,
            SentOn = email.SentOn,
            Subject = email.Subject,
            HtmlContent = mainContent,
            TextContent = HtmlToPlainText(mainContent),
            Index = 0,
            IsParent = true,
            EmbeddedImages = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages),
            Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
        });

        // Process each separator div - the separator contains the metadata and the content follows inside it
        for (int i = 0; i < separators.Count; i++)
        {
            var separator = separators[i];
            HtmlNode? nextSeparator = i < separators.Count - 1 ? separators[i + 1] : null;

            // The separator div itself contains the From: header and the email content
            // We need to extract the content from inside the separator, up to the next separator
            string quotedContent;
            
            if (nextSeparator != null)
            {
                // Extract content from this separator up to (but not including) the next separator
                quotedContent = ExtractContentFromSeparatorToNext(separator, nextSeparator);
            }
            else
            {
                // Last separator - extract all content from it
                quotedContent = separator.InnerHtml;
            }

            // Extract metadata from the separator's content
            var metadata = ExtractEmailMetadata(quotedContent);

            if (!string.IsNullOrWhiteSpace(quotedContent))
            {
                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? "Unknown",
                    To = metadata.To ?? email.From,
                    Cc = metadata.Cc ?? string.Empty,
                    SentOn = metadata.Date,
                    Subject = metadata.Subject ?? email.Subject,
                    HtmlContent = quotedContent,
                    TextContent = HtmlToPlainText(quotedContent),
                    Index = correspondences.Count,
                    IsParent = false,
                    EmbeddedImages = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages)
                });
            }
        }

        return correspondences;
    }

    /// <summary>
    /// Extract content from a separator div up to (but not including) the next separator
    /// </summary>
    private string ExtractContentFromSeparatorToNext(HtmlNode startSeparator, HtmlNode endSeparator)
    {
        var content = new StringBuilder();
        
        // Check if endSeparator is nested inside startSeparator's content
        if (ContainsNode(startSeparator, endSeparator))
        {
            // The next separator is inside this separator - extract content before it
            ExtractBeforeNodeInSubtree(startSeparator, endSeparator, content);
        }
        else
        {
            // The separators are at different levels - include all of startSeparator's content
            content.Append(startSeparator.InnerHtml);
        }
        
        return content.ToString();
    }

    private List<Correspondence> ExtractWithHrSeparators(EmailMessage email, HtmlDocument doc, HtmlNodeCollection separators)
    {
        var correspondences = new List<Correspondence>();

        var mainContent = ExtractContentBeforeNode(doc.DocumentNode, separators[0]);

        correspondences.Add(new Correspondence
        {
            From = email.From,
            To = email.To,
            Cc = email.Cc,
            SentOn = email.SentOn,
            Subject = email.Subject,
            HtmlContent = mainContent,
            TextContent = HtmlToPlainText(mainContent),
            Index = 0,
            IsParent = true,
            EmbeddedImages = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages),
            Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
        });

        for (int i = 0; i < separators.Count; i++)
        {
            HtmlNode? nextSeparator = i < separators.Count - 1 ? separators[i + 1] : null;
            var quotedContent = ExtractContentBetweenNodes(separators[i], nextSeparator);

            if (!string.IsNullOrWhiteSpace(quotedContent))
            {
                var metadata = ExtractEmailMetadata(quotedContent);

                correspondences.Add(new Correspondence
                {
                    From = metadata.From ?? "Unknown",
                    To = metadata.To ?? email.From,
                    Cc = metadata.Cc ?? string.Empty,
                    SentOn = metadata.Date,
                    Subject = metadata.Subject ?? email.Subject,
                    HtmlContent = quotedContent,
                    TextContent = HtmlToPlainText(quotedContent),
                    Index = i + 1,
                    IsParent = false,
                    EmbeddedImages = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages)
                });
            }
        }

        return correspondences;
    }

    private List<Correspondence> ExtractWithQuoteBlocks(EmailMessage email, HtmlNodeCollection quoteBlocks)
    {
        var correspondences = new List<Correspondence>();
        var doc = quoteBlocks[0].OwnerDocument;

        var mainContent = ExtractContentBeforeNode(doc.DocumentNode, quoteBlocks[0]);

        correspondences.Add(new Correspondence
        {
            From = email.From,
            To = email.To,
            Cc = email.Cc,
            SentOn = email.SentOn,
            Subject = email.Subject,
            HtmlContent = mainContent,
            TextContent = HtmlToPlainText(mainContent),
            Index = 0,
            IsParent = true,
            EmbeddedImages = ExtractImagesForHtmlContent(mainContent, email.EmbeddedImages),
            Attachments = new Dictionary<string, byte[]>(email.AttachmentData)
        });

        int index = 1;
        foreach (var quoteBlock in quoteBlocks)
        {
            var quotedContent = quoteBlock.InnerHtml;
            var metadata = ExtractEmailMetadata(quotedContent);

            correspondences.Add(new Correspondence
            {
                From = metadata.From ?? "Unknown",
                To = metadata.To ?? email.From,
                Cc = metadata.Cc ?? string.Empty,
                SentOn = metadata.Date,
                Subject = metadata.Subject ?? email.Subject,
                HtmlContent = quotedContent,
                TextContent = HtmlToPlainText(quotedContent),
                Index = index++,
                IsParent = false,
                EmbeddedImages = ExtractImagesForHtmlContent(quotedContent, email.EmbeddedImages)
            });
        }

        return correspondences;
    }

    private List<Correspondence> SplitByFromPattern(EmailMessage email)
    {
        var correspondences = new List<Correspondence>();

        // Build regex pattern for all "From:" variants
        var fromPatternString = string.Join("|", FromPatterns.Select(p => Regex.Escape(p)));
        var splitPattern = $@"(?=<(?:b|strong|span)(?:\s+[^>]*)?>(?:{fromPatternString}):</(?:b|strong|span)>)";

        var sections = Regex.Split(email.HtmlBody, splitPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        for (int i = 0; i < sections.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(sections[i]))
            {
                var metadata = ExtractEmailMetadata(sections[i]);

                var fromAddress = metadata.From ?? (i == 0 ? email.From : "Unknown");
                var toAddress = metadata.To ?? (i == 0 ? email.To : email.From);
                var sentDate = metadata.Date ?? (i == 0 ? email.SentOn : null);

                correspondences.Add(new Correspondence
                {
                    From = fromAddress,
                    To = toAddress,
                    Cc = metadata.Cc ?? string.Empty,
                    SentOn = sentDate,
                    Subject = email.Subject,
                    HtmlContent = sections[i],
                    TextContent = HtmlToPlainText(sections[i]),
                    Index = i,
                    IsParent = i == 0,
                    EmbeddedImages = ExtractImagesForHtmlContent(sections[i], email.EmbeddedImages),
                    Attachments = i == 0 ? new Dictionary<string, byte[]>(email.AttachmentData) : []
                });
            }
        }

        return correspondences;
    }

    private (string? From, string? To, string? Cc, DateTime? Date, string? Subject) ExtractEmailMetadata(string htmlContent)
    {
        string? from = null;
        string? to = null;
        string? cc = null;
        DateTime? date = null;
        string? subject = null;

        var text = HtmlToPlainText(htmlContent);

        // Build multi-language regex patterns
        var fromPattern = $@"(?:{string.Join("|", FromPatterns.Select(p => Regex.Escape(p)))}):\s*(.+?)(?:\r?\n|{string.Join("|", SentPatterns.Concat(ToPatterns).Concat(SubjectPatterns).Concat(CcPatterns).Select(p => Regex.Escape(p) + ":"))}|$)";
        var toPattern = $@"(?:{string.Join("|", ToPatterns.Select(p => Regex.Escape(p)))}):\s*(.+?)(?:\r?\n|{string.Join("|", CcPatterns.Concat(SubjectPatterns).Concat(SentPatterns).Select(p => Regex.Escape(p) + ":"))}|$)";
        var ccPattern = $@"(?:{string.Join("|", CcPatterns.Select(p => Regex.Escape(p)))}):\s*(.+?)(?:\r?\n|{string.Join("|", SubjectPatterns.Concat(SentPatterns).Select(p => Regex.Escape(p) + ":"))}|$)";
        var sentPattern = $@"(?:{string.Join("|", SentPatterns.Select(p => Regex.Escape(p)))}):\s*(.+?)(?:\r?\n|{string.Join("|", ToPatterns.Concat(FromPatterns).Concat(SubjectPatterns).Concat(CcPatterns).Select(p => Regex.Escape(p) + ":"))}|$)";
        var subjectPattern = $@"(?:{string.Join("|", SubjectPatterns.Select(p => Regex.Escape(p)))}):\s*(.+?)(?:\r?\n|{string.Join("|", FromPatterns.Concat(ToPatterns).Concat(SentPatterns).Concat(CcPatterns).Select(p => Regex.Escape(p) + ":"))}|$)";

        var fromMatch = Regex.Match(text, fromPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (fromMatch.Success)
        {
            from = Regex.Replace(fromMatch.Groups[1].Value.Trim(), @"\s+", " ");
        }

        var toMatch = Regex.Match(text, toPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (toMatch.Success)
        {
            to = Regex.Replace(toMatch.Groups[1].Value.Trim(), @"\s+", " ");
        }

        var ccMatch = Regex.Match(text, ccPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (ccMatch.Success)
        {
            cc = Regex.Replace(ccMatch.Groups[1].Value.Trim(), @"\s+", " ");
        }

        var dateMatch = Regex.Match(text, sentPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (dateMatch.Success)
        {
            var dateStr = dateMatch.Groups[1].Value.Trim();

            if (DateTime.TryParse(dateStr, out var parsedDate))
            {
                date = parsedDate;
            }
            else
            {
                var cleanedDateStr = dateStr.Replace(" at ", " ").Replace(" à ", " ").Replace(" um ", " ");
                if (DateTime.TryParse(cleanedDateStr, out parsedDate))
                {
                    date = parsedDate;
                }
            }
        }

        var subjectMatch = Regex.Match(text, subjectPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (subjectMatch.Success)
        {
            subject = Regex.Replace(subjectMatch.Groups[1].Value.Trim(), @"\s+", " ");
        }

        return (from, to, cc, date, subject);
    }

    private Dictionary<string, byte[]> ExtractImagesForHtmlContent(string htmlContent, Dictionary<string, byte[]> allEmbeddedImages)
    {
        var imagesForContent = new Dictionary<string, byte[]>();

        if (string.IsNullOrWhiteSpace(htmlContent) || allEmbeddedImages.Count == 0)
        {
            return imagesForContent;
        }

        var cidPattern = @"(?:src|background)\s*=\s*['""]cid:([^'""]+)['""]";
        var matches = Regex.Matches(htmlContent, cidPattern, RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var contentId = match.Groups[1].Value;

            if (allEmbeddedImages.TryGetValue(contentId, out var imageData))
            {
                imagesForContent[contentId] = imageData;
            }
        }

        return imagesForContent;
    }

    private string ExtractContentBeforeNode(HtmlNode rootNode, HtmlNode beforeNode)
    {
        var content = new StringBuilder();

        foreach (var node in rootNode.ChildNodes)
        {
            if (node == beforeNode)
                break;

            content.Append(node.OuterHtml);
        }

        return content.ToString();
    }

    private string ExtractAllContentBeforeSeparator(HtmlDocument doc, HtmlNode separator)
    {
        var content = new StringBuilder();

        foreach (var topNode in doc.DocumentNode.ChildNodes)
        {
            if (ContainsNode(topNode, separator))
            {
                ExtractBeforeNodeInSubtree(topNode, separator, content);
                break;
            }
            else
            {
                content.Append(topNode.OuterHtml);
            }
        }

        return content.ToString();
    }

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

    private void ExtractBeforeNodeInSubtree(HtmlNode node, HtmlNode target, StringBuilder content)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child == target)
            {
                return;
            }

            if (ContainsNode(child, target))
            {
                ExtractBeforeNodeInSubtree(child, target, content);
                return;
            }
            else
            {
                content.Append(child.OuterHtml);
            }
        }
    }

    private string ExtractContentAfterNode(HtmlNode afterNode)
    {
        var content = new StringBuilder();
        var currentNode = afterNode.NextSibling;

        while (currentNode != null)
        {
            if (currentNode.Name == "hr" ||
                currentNode.Attributes["style"]?.Value?.Contains("border-top") == true)
                break;

            content.Append(currentNode.OuterHtml);
            currentNode = currentNode.NextSibling;
        }

        return content.ToString();
    }

    private string ExtractContentBetweenNodes(HtmlNode startNode, HtmlNode? endNode)
    {
        var content = new StringBuilder();

        var startParent = startNode.ParentNode;
        if (startParent == null)
            return string.Empty;

        var endParent = endNode?.ParentNode;

        var currentNode = startParent.NextSibling;

        while (currentNode != null)
        {
            if (endParent != null && currentNode == endParent)
                break;

            if (currentNode.NodeType == HtmlNodeType.Text &&
                string.IsNullOrWhiteSpace(currentNode.InnerText))
            {
                currentNode = currentNode.NextSibling;
                continue;
            }

            content.Append(currentNode.OuterHtml);
            currentNode = currentNode.NextSibling;
        }

        return content.ToString();
    }

    private Correspondence CreateSingleCorrespondence(EmailMessage email)
    {
        return new Correspondence
        {
            From = email.From,
            To = email.To,
            Cc = email.Cc,
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

    private string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return doc.DocumentNode.InnerText;
    }
}
