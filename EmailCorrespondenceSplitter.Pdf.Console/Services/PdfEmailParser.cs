using System.Text;
using System.Text.RegularExpressions;
using EmailCorrespondenceSplitter.Pdf.Console.Models;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace EmailCorrespondenceSplitter.Pdf.Console.Services;

/// <summary>
/// Parser for PDF email files with style extraction
/// </summary>
public class PdfEmailParser : IEmailParser
{
    public async Task<EmailMessage> ParseAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            using var pdfReader = new PdfReader(filePath);
            using var pdfDocument = new PdfDocument(pdfReader);
            
            // Extract text content with styles and page information
            var (htmlContent, textContent, pageTextRanges) = ExtractTextWithStyles(pdfDocument);
            
            // Extract all images from the PDF with page information
            var images = ExtractImagesWithPageInfo(pdfDocument);
            
            if (images.Count > 0)
            {
                System.Console.WriteLine($"    Extracted {images.Count} image(s) from PDF");
            }
            
            // Try to extract email metadata from the text
            var metadata = ExtractEmailMetadata(textContent);
            
            var emailMessage = new EmailMessage
            {
                Subject = metadata.Subject ?? Path.GetFileNameWithoutExtension(filePath),
                From = metadata.From ?? string.Empty,
                To = metadata.To ?? string.Empty,
                Cc = metadata.Cc ?? string.Empty,
                SentOn = metadata.Date,
                HtmlBody = htmlContent,
                TextBody = textContent,
                FilePath = filePath,
                EmailType = EmailType.Generic
            };
            
            // Store extracted images with page info
            foreach (var image in images)
            {
                var imageId = $"pdf_image_p{image.PageNumber}_i{image.Index}";
                emailMessage.EmbeddedImages[imageId] = image.Data;
            }
            
            // Store page ranges for correspondence distribution
            emailMessage.CustomData["PageTextRanges"] = pageTextRanges;

            return emailMessage;
        });
    }

    public bool CanParse(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extract text content with styles (bold, italic, font size, color)
    /// </summary>
    private (string HtmlContent, string TextContent, List<(int PageNumber, int StartIndex, int EndIndex)> PageRanges) 
        ExtractTextWithStyles(PdfDocument pdfDocument)
    {
        var htmlBuilder = new StringBuilder();
        var textBuilder = new StringBuilder();
        var pageRanges = new List<(int PageNumber, int StartIndex, int EndIndex)>();

        for (int pageNum = 1; pageNum <= pdfDocument.GetNumberOfPages(); pageNum++)
        {
            var textStartIndex = textBuilder.Length;
            
            try
            {
                var page = pdfDocument.GetPage(pageNum);
                
                // Try styled extraction first
                var listener = new StyledTextExtractionListener();
                var processor = new PdfCanvasProcessor(listener);
                processor.ProcessPageContent(page);
                
                var pageHtml = listener.GetHtmlContent();
                var pageText = listener.GetPlainText();
                
                // Check if we got content
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    htmlBuilder.Append(pageHtml);
                    textBuilder.AppendLine(pageText);
                }
                else
                {
                    // Fallback to simple extraction
                    var strategy = new SimpleTextExtractionStrategy();
                    var simpleText = PdfTextExtractor.GetTextFromPage(page, strategy);
                    
                    var escapedText = System.Net.WebUtility.HtmlEncode(simpleText);
                    htmlBuilder.AppendLine($"<div class=\"page\">{escapedText.Replace("\n", "<br/>")}</div>");
                    textBuilder.AppendLine(simpleText);
                }
                
                if (pageNum < pdfDocument.GetNumberOfPages())
                {
                    htmlBuilder.AppendLine();
                    textBuilder.AppendLine();
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"    Warning: Error on page {pageNum}: {ex.Message}");
                
                // Fallback to simple text extraction
                try
                {
                    var page = pdfDocument.GetPage(pageNum);
                    var strategy = new SimpleTextExtractionStrategy();
                    var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                    
                    var escapedText = System.Net.WebUtility.HtmlEncode(pageText);
                    htmlBuilder.AppendLine($"<div class=\"page\">{escapedText.Replace("\n", "<br/>")}</div>");
                    textBuilder.AppendLine(pageText);
                }
                catch
                {
                    // Skip this page
                }
            }
            
            pageRanges.Add((pageNum, textStartIndex, textBuilder.Length));
        }

        return (htmlBuilder.ToString(), textBuilder.ToString(), pageRanges);
    }

    /// <summary>
    /// Extract images with page information
    /// </summary>
    private List<(int Index, int PageNumber, byte[] Data)> ExtractImagesWithPageInfo(PdfDocument pdfDocument)
    {
        var images = new List<(int Index, int PageNumber, byte[] Data)>();
        var processedImages = new HashSet<int>();
        int globalIndex = 0;

        for (int pageNum = 1; pageNum <= pdfDocument.GetNumberOfPages(); pageNum++)
        {
            try
            {
                var page = pdfDocument.GetPage(pageNum);
                var pageImages = new List<byte[]>();
                var listener = new ImageRenderListener(pageImages, processedImages);
                var processor = new PdfCanvasProcessor(listener);
                processor.ProcessPageContent(page);
                
                foreach (var imageData in pageImages)
                {
                    images.Add((globalIndex++, pageNum, imageData));
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"    Warning: Could not extract images from page {pageNum}: {ex.Message}");
            }
        }

        return images;
    }

    /// <summary>
    /// Extract email metadata from text content
    /// </summary>
    private (string? From, string? To, string? Cc, DateTime? Date, string? Subject) ExtractEmailMetadata(string text)
    {
        string? from = null;
        string? to = null;
        string? cc = null;
        DateTime? date = null;
        string? subject = null;

        var fromPatterns = new[] { "From", "Von", "De", "Da", "??", "Od", "Från", "Fra" };
        var toPatterns = new[] { "To", "An", "À", "A", "????", "Do", "Till", "Til" };
        var ccPatterns = new[] { "Cc", "CC", "Kopie", "Copie", "Copia", "?????", "Kopia" };
        var sentPatterns = new[] { "Sent", "Date", "Gesendet", "Envoyé", "Enviado", "Inviato", "??????????", "Datum" };
        var subjectPatterns = new[] { "Subject", "Betreff", "Objet", "Asunto", "Oggetto", "????", "Temat", "Ämne", "Emne" };

        var fromPattern = $@"(?:{string.Join("|", fromPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var toPattern = $@"(?:{string.Join("|", toPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var ccPattern = $@"(?:{string.Join("|", ccPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var sentPattern = $@"(?:{string.Join("|", sentPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";
        var subjectPattern = $@"(?:{string.Join("|", subjectPatterns.Select(Regex.Escape))}):\s*(.+?)(?:\r?\n|$)";

        var fromMatch = Regex.Match(text, fromPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (fromMatch.Success) from = fromMatch.Groups[1].Value.Trim();

        var toMatch = Regex.Match(text, toPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (toMatch.Success) to = toMatch.Groups[1].Value.Trim();

        var ccMatch = Regex.Match(text, ccPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (ccMatch.Success) cc = ccMatch.Groups[1].Value.Trim();

        var dateMatch = Regex.Match(text, sentPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (dateMatch.Success)
        {
            var dateStr = dateMatch.Groups[1].Value.Trim();
            if (DateTime.TryParse(dateStr, out var parsedDate))
                date = parsedDate;
        }

        var subjectMatch = Regex.Match(text, subjectPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (subjectMatch.Success) subject = subjectMatch.Groups[1].Value.Trim();

        return (from, to, cc, date, subject);
    }

    /// <summary>
    /// Styled text extraction listener
    /// </summary>
    private class StyledTextExtractionListener : IEventListener
    {
        private readonly List<TextChunk> _chunks = [];
        private const float LINE_THRESHOLD = 5f;
        private const float SPACE_THRESHOLD = 3f;

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type != EventType.RENDER_TEXT)
                return;

            try
            {
                var renderInfo = (TextRenderInfo)data;
                var text = renderInfo.GetText();
                
                if (string.IsNullOrEmpty(text))
                    return;

                var baseline = renderInfo.GetBaseline();
                var startPoint = baseline.GetStartPoint();
                float x = startPoint.Get(0);
                float y = startPoint.Get(1);
                float fontSize = renderInfo.GetFontSize();
                
                // Detect font style
                var font = renderInfo.GetFont();
                bool isBold = IsBoldFont(font);
                bool isItalic = IsItalicFont(font);
                
                // Get color
                string colorHex = "#000000";
                try
                {
                    var fillColor = renderInfo.GetFillColor();
                    colorHex = GetColorHex(fillColor);
                }
                catch { }
                
                _chunks.Add(new TextChunk
                {
                    Text = text,
                    X = x,
                    Y = y,
                    FontSize = fontSize,
                    IsBold = isBold,
                    IsItalic = isItalic,
                    ColorHex = colorHex
                });
            }
            catch
            {
                // Ignore individual errors
            }
        }

        public ICollection<EventType> GetSupportedEvents() => [EventType.RENDER_TEXT];

        public string GetHtmlContent()
        {
            if (_chunks.Count == 0)
                return string.Empty;

            var sorted = _chunks
                .OrderByDescending(c => c.Y)
                .ThenBy(c => c.X)
                .ToList();

            var html = new StringBuilder();
            float lastY = float.MaxValue;
            float lastX = 0;
            bool inParagraph = false;

            foreach (var chunk in sorted)
            {
                bool newLine = Math.Abs(chunk.Y - lastY) > LINE_THRESHOLD;
                
                if (newLine)
                {
                    if (inParagraph)
                        html.Append("</p>");
                    html.Append("<p>");
                    inParagraph = true;
                    lastX = 0;
                }
                else if (chunk.X - lastX > SPACE_THRESHOLD && lastX > 0)
                {
                    html.Append(" ");
                }

                var escapedText = System.Net.WebUtility.HtmlEncode(chunk.Text);
                
                // Apply styles
                var styles = new List<string>();
                if (chunk.FontSize > 14)
                    styles.Add($"font-size:{chunk.FontSize:F0}pt");
                if (chunk.ColorHex != "#000000")
                    styles.Add($"color:{chunk.ColorHex}");

                string styledText = escapedText;
                if (chunk.IsBold) styledText = $"<strong>{styledText}</strong>";
                if (chunk.IsItalic) styledText = $"<em>{styledText}</em>";

                if (styles.Count > 0)
                    html.Append($"<span style=\"{string.Join(";", styles)}\">{styledText}</span>");
                else
                    html.Append(styledText);

                lastY = chunk.Y;
                lastX = chunk.X + (chunk.Text.Length * chunk.FontSize * 0.5f);
            }

            if (inParagraph)
                html.Append("</p>");

            return html.ToString();
        }

        public string GetPlainText()
        {
            if (_chunks.Count == 0)
                return string.Empty;

            var sorted = _chunks
                .OrderByDescending(c => c.Y)
                .ThenBy(c => c.X)
                .ToList();

            var text = new StringBuilder();
            float lastY = float.MaxValue;
            float lastX = 0;

            foreach (var chunk in sorted)
            {
                if (Math.Abs(chunk.Y - lastY) > LINE_THRESHOLD)
                {
                    if (text.Length > 0)
                        text.AppendLine();
                    lastX = 0;
                }
                else if (chunk.X - lastX > SPACE_THRESHOLD && lastX > 0)
                {
                    text.Append(" ");
                }

                text.Append(chunk.Text);
                lastY = chunk.Y;
                lastX = chunk.X + (chunk.Text.Length * chunk.FontSize * 0.5f);
            }

            return text.ToString();
        }

        private static bool IsBoldFont(PdfFont? font)
        {
            try
            {
                var fontName = font?.GetFontProgram()?.GetFontNames()?.GetFontName() ?? "";
                return fontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
                       fontName.Contains("Black", StringComparison.OrdinalIgnoreCase) ||
                       fontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool IsItalicFont(PdfFont? font)
        {
            try
            {
                var fontName = font?.GetFontProgram()?.GetFontNames()?.GetFontName() ?? "";
                return fontName.Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
                       fontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string GetColorHex(Color? color)
        {
            try
            {
                if (color == null) return "#000000";
                var values = color.GetColorValue();
                if (values == null || values.Length == 0) return "#000000";

                if (values.Length >= 3)
                {
                    int r = (int)(values[0] * 255);
                    int g = (int)(values[1] * 255);
                    int b = (int)(values[2] * 255);
                    return $"#{r:X2}{g:X2}{b:X2}";
                }
                else if (values.Length == 1)
                {
                    int gray = (int)(values[0] * 255);
                    return $"#{gray:X2}{gray:X2}{gray:X2}";
                }
                return "#000000";
            }
            catch { return "#000000"; }
        }

        private class TextChunk
        {
            public string Text { get; set; } = "";
            public float X { get; set; }
            public float Y { get; set; }
            public float FontSize { get; set; }
            public bool IsBold { get; set; }
            public bool IsItalic { get; set; }
            public string ColorHex { get; set; } = "#000000";
        }
    }

    /// <summary>
    /// Image extraction listener
    /// </summary>
    private class ImageRenderListener : IEventListener
    {
        private readonly List<byte[]> _images;
        private readonly HashSet<int> _processedHashes;

        public ImageRenderListener(List<byte[]> images, HashSet<int> processedHashes)
        {
            _images = images;
            _processedHashes = processedHashes;
        }

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type != EventType.RENDER_IMAGE)
                return;

            try
            {
                var renderInfo = (ImageRenderInfo)data;
                var imageObject = renderInfo.GetImage();
                
                if (imageObject == null)
                    return;

                byte[]? imageBytes = null;
                try { imageBytes = imageObject.GetImageBytes(true); }
                catch { try { imageBytes = imageObject.GetImageBytes(false); } catch { return; } }
                
                if (imageBytes != null && imageBytes.Length > 500)
                {
                    var hash = GetHash(imageBytes);
                    if (!_processedHashes.Contains(hash))
                    {
                        _processedHashes.Add(hash);
                        _images.Add(imageBytes);
                    }
                }
            }
            catch { }
        }

        public ICollection<EventType> GetSupportedEvents() => [EventType.RENDER_IMAGE];

        private static int GetHash(byte[] data)
        {
            unchecked
            {
                int hash = 17;
                int step = Math.Max(1, data.Length / 100);
                for (int i = 0; i < data.Length; i += step)
                    hash = hash * 31 + data[i];
                return hash * 31 + data.Length;
            }
        }
    }
}
