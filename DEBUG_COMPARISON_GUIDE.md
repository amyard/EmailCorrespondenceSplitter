# Debug Comparison Guide

## How to Use This Guide

Run your application and look for the DEBUG output lines. This will help you compare the OLD and NEW implementations.

---

## What to Look For

### 1. Text Extraction Comparison

**OLD Implementation (PdfCorrespondenceDetector)**:
```
DEBUG OLD: Text body length: XXXX characters
```

**NEW Implementation (PdfDirectCorrespondenceSplitter)**:
```
DEBUG: Extracted XXXX characters from PDF
```

? **Check**: Are the character counts similar? If they differ significantly, the text extraction method is different.

---

### 2. Page Mapping

**OLD Implementation**:
```
DEBUG OLD: Page ranges from email: X
DEBUG OLD: Page 1: chars 0-500
DEBUG OLD: Page 2: chars 500-1200
```

**NEW Implementation**:
```
DEBUG: Page ranges count: X
DEBUG: Page 1: chars 0-500
DEBUG: Page 2: chars 500-1200
```

? **Check**: Do the page ranges match? The character positions should be identical or very similar.

---

### 3. Correspondence Detection

**OLD Implementation**:
```
DEBUG OLD: Found X text sections:
DEBUG OLD: Section 1: chars 0-800 (length: 800)
DEBUG OLD: Preview: From: john@example.com...
DEBUG OLD: Section 2: chars 800-1500 (length: 700)
DEBUG OLD: Preview: From: jane@example.com...
```

**NEW Implementation**:
```
DEBUG: Found X correspondence boundaries:
DEBUG: Correspondence 1: chars 0-800 (length: 800)
DEBUG: Preview: From: john@example.com...
DEBUG: Correspondence 2: chars 800-1500 (length: 700)
DEBUG: Preview: From: jane@example.com...
```

? **Check**: 
- Is the count the same?
- Are the character ranges identical?
- Do the previews show the same "From:" lines?

---

### 4. Page Mapping (NEW Only)

**NEW Implementation**:
```
DEBUG: Mapped to X page ranges:
DEBUG: Correspondence 1: pages 1-2
DEBUG: Correspondence 2: pages 3-4
DEBUG: Correspondence 3: pages 5-5
```

? **Check**: Do the page ranges make sense given the character positions?

---

## Common Issues and Solutions

### Issue 1: Different Character Counts

**Symptom**:
```
DEBUG OLD: Text body length: 5000 characters
DEBUG: Extracted 5200 characters from PDF
```

**Cause**: The NEW implementation adds newlines between pages (`AppendLine`), while the OLD might not.

**Solution**: Both should use the same text extraction. The OLD gets text from `email.TextBody` which comes from `PdfEmailParser`. Make sure they're using the same source.

---

### Issue 2: Different Section Counts

**Symptom**:
```
DEBUG OLD: Found 3 text sections
DEBUG: Found 2 correspondence boundaries
```

**Cause**: The regex pattern or matching logic differs.

**Solution**: 
- Check if both use the same `FromPatterns` array
- Verify the regex pattern is identical
- Check if one includes/excludes empty sections

---

### Issue 3: Different Character Positions

**Symptom**:
```
DEBUG OLD: Section 1: chars 0-800
DEBUG: Correspondence 1: chars 0-850
```

**Cause**: The splitting logic differs slightly.

**Solution**: The OLD implementation's `SplitWithPositions` might filter out whitespace that the NEW doesn't.

---

### Issue 4: Wrong Page Mapping

**Symptom**:
```
DEBUG: Correspondence 1: chars 0-800
DEBUG: Page 1: chars 0-500
DEBUG: Page 2: chars 500-1200
DEBUG: Correspondence 1: pages 1-1  ? Should be 1-2!
```

**Cause**: The `MapTextBoundariesToPages` logic has a bug.

**Solution**: The correspondence spans pages 1-2 (chars 0-800 covers 0-500 and 500-800), but it's only mapping to page 1.

---

## Expected Output Example

For a PDF with 3 correspondences:

```
Processing PDF: em1.pdf
  Subject: Test Email
  From: john@example.com
  
  [OLD METHOD]
  DEBUG OLD: Text body length: 3500 characters
  DEBUG OLD: Found 3 text sections:
  DEBUG OLD: Section 1: chars 0-1200 (length: 1200)
  DEBUG OLD: Preview: From: john@example.com Sent: Monday...
  DEBUG OLD: Section 2: chars 1200-2400 (length: 1200)
  DEBUG OLD: Preview: From: jane@example.com Sent: Tuesday...
  DEBUG OLD: Section 3: chars 2400-3500 (length: 1100)
  DEBUG OLD: Preview: From: bob@example.com Sent: Wednesday...
  DEBUG OLD: Page ranges from email: 5
  DEBUG OLD: Page 1: chars 0-700
  DEBUG OLD: Page 2: chars 700-1400
  DEBUG OLD: Page 3: chars 1400-2100
  DEBUG OLD: Page 4: chars 2100-2800
  DEBUG OLD: Page 5: chars 2800-3500
  DEBUG OLD: Created 3 correspondence objects
  
  [NEW METHOD]
  DEBUG: Extracted 3500 characters from PDF
  DEBUG: Page ranges count: 5
  DEBUG: Page 1: chars 0-700
  DEBUG: Page 2: chars 700-1400
  DEBUG: Page 3: chars 1400-2100
  DEBUG: Page 4: chars 2100-2800
  DEBUG: Page 5: chars 2800-3500
  DEBUG: Found 3 correspondence boundaries:
  DEBUG: Correspondence 1: chars 0-1200 (length: 1200)
  DEBUG: Preview: From: john@example.com Sent: Monday...
  DEBUG: Correspondence 2: chars 1200-2400 (length: 1200)
  DEBUG: Preview: From: jane@example.com Sent: Tuesday...
  DEBUG: Correspondence 3: chars 2400-3500 (length: 1100)
  DEBUG: Preview: From: bob@example.com Sent: Wednesday...
  DEBUG: Mapped to 3 page ranges:
  DEBUG: Correspondence 1: pages 1-2  ? (chars 0-1200 spans pages 1-2)
  DEBUG: Correspondence 2: pages 2-4  ? (chars 1200-2400 spans pages 2-4)
  DEBUG: Correspondence 3: pages 4-5  ? (chars 2400-3500 spans pages 4-5)
  Found 3 correspondence(s) in text
  Saved new correspondence 1 (pages 1-2): 01_correspondence.pdf
  Saved new correspondence 2 (pages 2-4): 02_correspondence.pdf
  Saved new correspondence 3 (pages 4-5): 03_correspondence.pdf
```

---

## Next Steps

1. **Run the application** with a test PDF file
2. **Copy the console output** with all DEBUG lines
3. **Compare the values** using this guide
4. **Identify the mismatch** (character counts, section counts, positions, page ranges)
5. **Report back** with the specific issue found

---

## Quick Fix Checklist

If they don't match, check:

- [ ] Are both using the same text source? (email.TextBody)
- [ ] Do both use the same FromPatterns array?
- [ ] Is the regex pattern identical?
- [ ] Does page mapping logic correctly span multiple pages?
- [ ] Are character positions calculated the same way?
