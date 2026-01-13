# Email Correspondence Splitter - PDF Console

This project extracts individual email correspondences from MSG files and saves each as a separate PDF file using iText7.

## Features

- **Universal Email Support**: Works with emails from any email client (Outlook, Gmail, Apple Mail, Thunderbird, Yahoo, etc.)
- **Simple Pattern Matching**: Splits correspondences by detecting "From: ..." patterns in the email body
- **PDF Generation with iText7**: Converts each correspondence into a well-formatted PDF file
- **Organized Folder Structure**: Creates a dedicated folder for each email containing all its correspondence PDFs
- **Batch Processing**: Processes all MSG files in the Assets directory

## How It Works

1. **Email Reading**: Reads MSG (Outlook message) files from the `Assets` folder
2. **Correspondence Detection**: Splits the email content by detecting "From: ..." headers
   - Each section starting with "From: ..." is treated as a separate correspondence
   - If no "From:" patterns are found, the entire email is treated as a single correspondence
3. **PDF Generation**: Creates a folder for each email and generates PDF files inside with:
   - Subject
   - From address
   - To address
   - Date
   - Email content

## Project Structure

```
EmailCorrespondenceSplitter.Pdf.Console/
??? Assets/                          # Input directory (MSG files)
??? Output/                          # Output directory (email folders with PDFs)
?   ??? em1/                        # Folder for email em1.msg
?   ?   ??? correspondence_1.pdf
?   ?   ??? correspondence_2.pdf
?   ?   ??? correspondence_3.pdf
?   ??? em2/                        # Folder for email em2.msg
?       ??? correspondence.pdf
??? Models/
?   ??? EmailCorrespondence.cs      # Data model for a single correspondence
??? Services/
?   ??? EmailReader.cs              # Reads MSG files and extracts text
?   ??? CorrespondenceExtractor.cs  # Splits email into correspondences
?   ??? PdfGenerator.cs             # Generates PDF files using iText7
?   ??? EmailCorrespondenceSplitterService.cs  # Main orchestration service
??? Program.cs                       # Entry point
```

## Usage

1. Place your MSG files in the `Assets` folder
2. Run the application
3. PDFs will be generated in the `Output` folder, organized by email

### Output Folder Structure

Each email gets its own folder named after the MSG file:

```
Output/
??? em1/
?   ??? correspondence_1.pdf
?   ??? correspondence_2.pdf
?   ??? correspondence_3.pdf
??? em2/
?   ??? correspondence.pdf
??? em3/
    ??? correspondence_1.pdf
    ??? correspondence_2.pdf
```

### File Naming Convention

- **Single correspondence**: `correspondence.pdf`
- **Multiple correspondences**: `correspondence_1.pdf`, `correspondence_2.pdf`, etc.

## Example Workflow

For an email file named `em1.msg` containing 3 correspondences:

1. A folder `Output/em1/` is created
2. Three PDFs are generated inside:
   - `correspondence_1.pdf` (most recent)
   - `correspondence_2.pdf`
   - `correspondence_3.pdf` (oldest)

## Dependencies

- **MsgReader** (5.5.0): For reading Outlook MSG files
- **iText7** (7.2.5): For generating professional PDF documents (AGPL compatible version)
- **HtmlAgilityPack** (1.11.71): For HTML to text conversion

## Technical Details

### Correspondence Detection

The `CorrespondenceExtractor` uses a simple regex pattern to detect correspondence boundaries:

```regex
(?:^|\r?\n)From:\s*(.+?)(?=\r?\n(?:From:|$)|$)
```

This pattern matches:
- "From:" at the beginning of a line (case-insensitive)
- Content until the next "From:" or end of text
- Works with various email formats and clients

### Metadata Extraction

For each correspondence, the extractor attempts to parse:
- **From**: Email sender (from "From: xxx" line)
- **To**: Email recipient (from "To: xxx" line)
- **Date**: Send date (from "Sent: xxx" or "Date: xxx" line)

### PDF Generation with iText7

The `PdfGenerator` uses iText7 version 7.2.5 to create professional PDFs with:
- Clear typography using Helvetica fonts
- Structured layout with labels and values
- Proper spacing and visual separators
- Automatic page management

**Note**: iText7 7.2.5 is used for better AGPL compatibility. Version 8.x requires commercial licensing for most use cases.

## Limitations

- Currently only supports MSG files (Outlook message format)
- Relies on "From:" patterns in the email body for splitting
- Does not preserve formatting, images, or attachments
- Converts HTML emails to plain text

## Future Enhancements

Possible improvements:
- Support for EML and other email formats
- Preserve HTML formatting in PDFs
- Include email attachments
- Support for embedded images
- More sophisticated correspondence detection algorithms
- Custom PDF styling and branding

## License Notes

**iText7 version 7.2.5** is licensed under AGPL. This means:
- ? Free for open-source projects
- ? Free for personal use
- ? Commercial use requires a commercial license from iText Software

For commercial projects, you may need to:
1. Purchase a commercial license from iText Software, OR
2. Release your project under AGPL license
