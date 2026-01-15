# Quick Reference Guide - PDF Correspondence Extraction Methods

## Summary
The application now supports **three methods** for extracting correspondences:

### 1. MSG Method (Suffix: `_msg`)
- **Used for**: MSG files
- **Output**: `01_correspondence_sender_msg.pdf`
- **Process**: MSG ? HTML/Text ? PDF
- **Preserves**: Email structure and formatting from MSG

### 2. OLD PDF Method (Suffix: `_old`)
- **Used for**: PDF files
- **Output**: `01_correspondence_sender_old.pdf`
- **Process**: PDF ? Extract Text ? HTML ? New PDF
- **Preserves**: Content with reconstructed formatting
- **Location**: Uses `PdfCorrespondenceDetector` + `PdfOutputManager.SaveCorrespondenceAsync()`

### 3. NEW PDF Method (Suffix: `_new`)
- **Used for**: PDF files
- **Output**: `01_correspondence_new.pdf`
- **Process**: PDF ? Detect "From:" boundaries ? Copy pages ? New PDF
- **Preserves**: 100% original PDF content (fonts, graphics, layout)
- **Location**: Uses `PdfDirectCorrespondenceSplitter`

## File Naming Convention

### MSG Files Output:
```
em1/                                          ? MSG method folder
??? 00_parent_original.msg                    ? Original file
??? 01_correspondence_sendername.pdf          ? Method: MSG
??? 02_correspondence_sendername.pdf          ? Method: MSG
```

### PDF Files Output:
```
em1_pdf/                                      ? OLD method folder
??? 00_parent_original.pdf                    ? Original file
??? 01_correspondence_sendername.pdf          ? Method: OLD (HTML conversion)
??? 02_correspondence_sendername.pdf          ? Method: OLD (HTML conversion)

em1_pdf_new/                                  ? NEW method folder
??? 00_parent_original.pdf                    ? Original file
??? 01_correspondence.pdf                     ? Method: NEW (direct split)
??? 02_correspondence.pdf                     ? Method: NEW (direct split)
```

## Key Differences

| Feature | MSG Method | OLD PDF Method | NEW PDF Method |
|---------|-----------|----------------|----------------|
| **Source File** | .msg | .pdf | .pdf |
| **Extraction** | MimeKit | iText7 text | iText7 pages |
| **Conversion** | HTML ? PDF | Text ? HTML ? PDF | Direct PDF copy |
| **Quality** | High | Medium | Perfect |
| **Speed** | Medium | Slow | Fast |
| **Formatting Loss** | Minimal | Some | None |
| **Vector Graphics** | N/A | May lose | Preserved |
| **Fonts** | Converted | May change | Original |
| **File Size** | Medium | Variable | Smallest |

## When to Use Each Method

### Use MSG Method:
- Processing original MSG email files
- Need to preserve email metadata
- Source is Outlook/Exchange format

### Use OLD PDF Method:
- Need to reorganize content
- Want to extract specific text/sections
- Converting to standardized format

### Use NEW PDF Method:
- Need perfect quality preservation
- Original PDF layout is critical
- Want fastest processing
- Best for archiving/legal purposes

## Code Integration

The application automatically runs all applicable methods:

```csharp
// For MSG files - only MSG method is used
await splitter.ProcessMsgEmailAsync("email.msg");
// Output: *_msg.pdf files

// For PDF files - both OLD and NEW methods are used
await splitter.ProcessPdfEmailAsync("email.pdf");
// Output: *_old.pdf and *_new.pdf files

// Process entire directory (all MSG and PDF files)
await splitter.ProcessDirectoryAsync("./Assets");
```

## Comparison Tips

To compare the three methods:
1. Process both MSG and PDF versions of the same email
2. Check output folder for all three suffixes
3. Compare:
   - Visual quality
   - File size
   - Processing time
   - Completeness of content

## Technical Notes

- **OLD method** uses `PdfCorrespondenceDetector.DetectCorrespondences()` to split by "From:" in extracted text
- **NEW method** uses `PdfDirectCorrespondenceSplitter.SplitPdfByCorrespondencesAsync()` to split by page boundaries
- Both methods support 12+ languages for "From:" detection
- NEW method can only split at page boundaries (limitation of PDF structure)
- If multiple correspondences are on same page, NEW method keeps them together
