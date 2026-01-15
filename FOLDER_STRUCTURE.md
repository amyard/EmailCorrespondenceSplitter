# Folder Structure - Visual Guide

## When Processing PDF Files

```
Output/
??? em1_pdf/                                         ? OLD method folder
?   ??? 00_parent_original.pdf                       ? Original PDF (copy)
?   ??? 01_correspondence_sender1.pdf                ? OLD method: HTML-to-PDF
?   ??? 02_correspondence_sender2.pdf                ? OLD method: HTML-to-PDF
?   ??? 03_correspondence_sender3.pdf                ? OLD method: HTML-to-PDF
?
??? em1_pdf_new/                                     ? NEW method folder
    ??? 00_parent_original.pdf                       ? Original PDF (copy)
    ??? 01_correspondence.pdf                        ? NEW method: Direct PDF split
    ??? 02_correspondence.pdf                        ? NEW method: Direct PDF split
    ??? 03_correspondence.pdf                        ? NEW method: Direct PDF split
```

## When Processing MSG Files

```
Output/
??? em1/                                             ? MSG method folder
    ??? 00_parent_original.msg                       ? Original MSG (copy)
    ??? 01_correspondence_sender1.pdf                ? MSG method: MSG-to-PDF
    ??? 02_correspondence_sender2.pdf                ? MSG method: MSG-to-PDF
    ??? 03_correspondence_sender3.pdf                ? MSG method: MSG-to-PDF
```

## Comparison Workflow

If you have the same email in both MSG and PDF format:

```
Output/
??? em1/                                             ? From MSG file
?   ??? 00_parent_original.msg
?   ??? 01_correspondence_sender1.pdf                ? Method 1: MSG parsing
?   ??? 02_correspondence_sender2.pdf
?
??? em1_pdf/                                         ? From PDF file (OLD)
?   ??? 00_parent_original.pdf
?   ??? 01_correspondence_sender1.pdf                ? Method 2: OLD PDF (HTML)
?   ??? 02_correspondence_sender2.pdf
?
??? em1_pdf_new/                                     ? From PDF file (NEW)
    ??? 00_parent_original.pdf
    ??? 01_correspondence.pdf                        ? Method 3: NEW PDF (direct)
    ??? 02_correspondence.pdf
```

## Key Benefits of Separate Folders

? **Complete Separation**: Each method has its own dedicated folder

? **Easy Comparison**: Compare entire folders (em1 vs em1_pdf vs em1_pdf_new)

? **Clear Identification**: Folder name instantly tells you which method was used

? **Independent Processing**: Can delete/archive any method without affecting others

? **Parallel Processing**: Can run multiple methods simultaneously

## File Naming Logic

| Folder | File Name | Method |
|--------|-----------|--------|
| `em1/` | `XX_correspondence_name.pdf` | MSG parsing |
| `em1_pdf/` | `XX_correspondence_name.pdf` | OLD PDF (HTML) |
| `em1_pdf_new/` | `XX_correspondence.pdf` | NEW PDF (direct) |

**Note**: The NEW method doesn't include sender name in filename because it preserves the original PDF pages exactly as-is, without parsing metadata.

## Usage Example

```csharp
// Process a PDF file - creates TWO folders
var splitter = new PdfEmailSplitter("./Output");
await splitter.ProcessPdfEmailAsync("em1.pdf");

// Result:
// - Output/em1_pdf/         (OLD method)
// - Output/em1_pdf_new/     (NEW method)

// Process an MSG file - creates ONE folder
await splitter.ProcessMsgEmailAsync("em1.msg");

// Result:
// - Output/em1/             (MSG method)
```

## Console Output Example

```
Processing PDF: em1.pdf
  Subject: Project Update
  From: john.doe@example.com
  Found 2 correspondence(s)
  Output folder (old method): C:\Output\em1_pdf
  Copied parent PDF
  Saving correspondences using old solution (HTML-to-PDF)...
  Saved correspondence 1 (old): john.doe@example.com
  Saved correspondence 2 (old): jane.smith@example.com
  Output folder (new method): C:\Output\em1_pdf_new
  Extracting correspondences directly from PDF (new solution)...
  Found 2 correspondence(s) in PDF based on 'From:' patterns
  Saved new correspondence 1 (pages 1-2): 01_correspondence.pdf
  Saved new correspondence 2 (pages 3-4): 02_correspondence.pdf
  Extracted 2 correspondence(s) using direct PDF splitting
  Successfully processed em1.pdf
