using System.Text;
using System.Text.RegularExpressions;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Service to extract correspondences from email content by splitting on "From: ..." patterns
/// </summary>
public class CorrespondenceExtractor
{
    /// <summary>
    /// Extract correspondences from plain text email content
    /// Each correspondence starts with "From: ..."
    /// </summary>
    /// <param name="emailContent">The full email content (plain text or HTML converted to text)</param>
    /// <param name="subject">Email subject</param>
    /// <returns>List of extracted correspondences</returns>
    public List<Models.EmailCorrespondence> ExtractCorrespondences(string emailContent, string subject)
    {
        var correspondences = new List<Models.EmailCorrespondence>();

        if (string.IsNullOrWhiteSpace(emailContent))
            return correspondences;

        // Pattern to match "From:" at the beginning of a line (case-insensitive)
        // This pattern looks for "From:" followed by content until the next "From:" or end of string
        var pattern = @"(?:^|\r?\n)From:\s*(.+?)(?=\r?\n(?:From:|$)|$)";
        var matches = Regex.Matches(emailContent, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // If no explicit "From:" patterns found, treat entire content as single correspondence
        if (matches.Count == 0)
        {
            // Try to extract metadata from the first few lines
            var metadata = ExtractMetadataFromContent(emailContent);
            correspondences.Add(new Models.EmailCorrespondence
            {
                From = metadata.From ?? "Unknown",
                To = metadata.To ?? "Unknown",
                SentDate = metadata.Date,
                Subject = subject,
                Content = emailContent,
                Index = 0
            });
            return correspondences;
        }

        // Extract each correspondence section
        int index = 0;
        foreach (Match match in matches)
        {
            // Find the start position of this "From:" line
            int startPos = match.Index;
            
            // Find the end position (next "From:" or end of content)
            int endPos = emailContent.Length;
            var nextFromMatch = Regex.Match(emailContent.Substring(startPos + match.Length), 
                @"\r?\nFrom:\s", RegexOptions.IgnoreCase);
            
            if (nextFromMatch.Success)
            {
                endPos = startPos + match.Length + nextFromMatch.Index;
            }

            // Extract the full correspondence text
            string correspondenceText = emailContent.Substring(startPos, endPos - startPos).Trim();
            
            // Extract metadata from this correspondence
            var metadata = ExtractMetadataFromContent(correspondenceText);

            correspondences.Add(new Models.EmailCorrespondence
            {
                From = metadata.From ?? "Unknown",
                To = metadata.To ?? "Unknown",
                SentDate = metadata.Date,
                Subject = subject,
                Content = correspondenceText,
                Index = index++
            });
        }

        return correspondences;
    }

    /// <summary>
    /// Extract metadata (From, To, Date) from correspondence text
    /// </summary>
    private (string? From, string? To, DateTime? Date) ExtractMetadataFromContent(string content)
    {
        string? from = null;
        string? to = null;
        DateTime? date = null;

        // Extract From - look for "From: xxx" pattern
        var fromMatch = Regex.Match(content, @"From:\s*(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase);
        if (fromMatch.Success)
        {
            from = fromMatch.Groups[1].Value.Trim();
            // Remove any trailing email header fields
            from = Regex.Replace(from, @"(Sent:|To:|Date:|Subject:).*$", "", RegexOptions.IgnoreCase).Trim();
        }

        // Extract To - look for "To: xxx" pattern
        var toMatch = Regex.Match(content, @"To:\s*(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase);
        if (toMatch.Success)
        {
            to = toMatch.Groups[1].Value.Trim();
            // Remove any trailing email header fields
            to = Regex.Replace(to, @"(Sent:|From:|Date:|Subject:|Cc:).*$", "", RegexOptions.IgnoreCase).Trim();
        }

        // Extract Date/Sent - look for "Sent: xxx" or "Date: xxx" pattern
        var dateMatch = Regex.Match(content, @"(?:Sent|Date):\s*(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase);
        if (dateMatch.Success)
        {
            var dateStr = dateMatch.Groups[1].Value.Trim();
            // Remove any trailing email header fields
            dateStr = Regex.Replace(dateStr, @"(To:|From:|Subject:).*$", "", RegexOptions.IgnoreCase).Trim();
            
            if (DateTime.TryParse(dateStr, out var parsedDate))
            {
                date = parsedDate;
            }
        }

        return (from, to, date);
    }
}
