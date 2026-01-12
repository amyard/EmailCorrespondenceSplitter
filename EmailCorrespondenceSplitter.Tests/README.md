# Email Correspondence Splitter - Unit Tests

This test project contains unit tests for the Email Correspondence Splitter application, which extracts individual correspondences from email threads and saves each as a separate file.

## Key Behavior

The application extracts **all correspondences** from an email (including the parent/original email) and saves each as an **individual HTML file** in a folder. No separate parent email file is created - the parent correspondence is saved as `01_correspondence_...html` with `IsParent: true` in its metadata.

## Test Structure

### CorrespondenceExtractionTests

This test class contains comprehensive tests for the email correspondence extraction functionality.

#### Key Test Methods

1. **ProcessEmail_ShouldExtractExpectedCorrespondenceCount**
   - Tests that the correct number of correspondences are extracted from email files
   - Uses `[Theory]` with `[InlineData]` to test multiple email files
   - Parameters:
     - `emailPath`: Path to the email file (relative to test output directory)
     - `expectedCount`: Expected number of correspondences to be extracted

2. **ProcessEmailWithSplitter_ShouldReturnExpectedCount**
   - Tests the complete EmailSplitter workflow
   - Verifies the end-to-end process including parsing, detection, and output
   - Same parameters as above

3. **ProcessEmailWithSplitter_ShouldCreateIndividualCorrespondenceFiles** *(NEW)*
   - Verifies that all correspondences are saved as individual files
   - Ensures no separate parent email file exists
   - All files should be named with "correspondence" pattern

4. **ProcessEmail_ShouldExtractCorrectMetadata**
   - Verifies that extracted correspondences contain correct metadata (From, Subject, IsParent, Index)

5. **ProcessEmail_WithInvalidPath_ShouldThrowException**
   - Tests error handling for invalid file paths

6. **CanParse_WithMsgFile_ShouldReturnTrue**
   - Tests that MSG files are correctly identified as parseable

7. **CanParse_WithNonMsgFile_ShouldReturnFalse**
   - Tests that non-MSG files are rejected

8. **ProcessEmail_AllCorrespondencesShouldHaveRequiredFields**
   - Ensures all correspondences have required fields populated

9. **ProcessEmail_FirstCorrespondenceShouldBeParent**
   - Verifies that the first correspondence (index 0) is always marked as the parent
   - The parent is still saved as an individual file, but flagged with `IsParent: true`

10. **ProcessEmail_CorrespondencesShouldBeNumberedSequentially** *(NEW)*
    - Tests that correspondence files are numbered sequentially (01_, 02_, etc.)
    - Ensures proper file ordering in output folders

## Output File Structure

When processing an email with 3 correspondences, the output will be:

```
Output/
  ??? email_name_20240101_120000/
      ??? 01_correspondence_sender1.html  (IsParent: true)
      ??? 02_correspondence_sender2.html  (IsParent: false)
      ??? 03_correspondence_sender3.html  (IsParent: false)
```

**Note:** No `00_parent_email.html` file is created. All correspondences, including the parent, are saved as individual correspondence files.

## Adding New Test Cases

To test a new email file, follow these steps:

1. **Add the email file to the Assets folder:**
   ```
   EmailCorrespondenceSplitter.Tests/Assets/your-email.msg
   ```

2. **Update the .csproj file to include the new file:**
   ```xml
   <None Update="Assets\your-email.msg">
     <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
   </None>
   ```

3. **Add a new InlineData attribute to the test methods:**
   ```csharp
   [InlineData("Assets/your-email.msg", expectedCorrespondenceCount)]
   ```

   Replace `expectedCorrespondenceCount` with the number of correspondences you expect to be extracted.

## Example: Adding a Test for a New Email

```csharp
[Theory]
[InlineData("Assets/em1.msg", 1)]
[InlineData("Assets/em2.msg", 1)]
[InlineData("Assets/new-email.msg", 3)]  // New test case: expects 3 correspondences
public async Task ProcessEmail_ShouldExtractExpectedCorrespondenceCount(string emailPath, int expectedCount)
{
    // Test implementation...
}
```

## Running the Tests

### Using Visual Studio
1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All" to run all tests
3. Or right-click on a specific test to run it individually

### Using .NET CLI
```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test
dotnet test --filter "FullyQualifiedName~ProcessEmail_ShouldExtractExpectedCorrespondenceCount"
```

## Test Data

The test project uses email files stored in the `Assets` folder. These files are automatically copied to the test output directory when the tests run.

Current test files:
- `em1.msg` through `em6.msg` - Sample email files for testing

## Notes

- The tests use xUnit as the testing framework
- The `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` is called in the static constructor to enable MSG file parsing
- Tests are designed to be independent and can run in parallel
- The `TestOutput` folder is used for output during tests and is automatically cleaned up
- **All correspondences (including parent) are saved as individual files** - no separate parent email file

## Troubleshooting

### Test fails with "File not found"
- Ensure the email file exists in the `Assets` folder
- Verify that the file is set to copy to the output directory in the `.csproj` file

### Test fails with encoding errors
- Ensure `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` is called before parsing MSG files

### Unexpected correspondence count
- Check the email structure and HTML content
- Verify the email type detection is working correctly (Gmail, Outlook, Apple)
- Review the correspondence detection logic in `CorrespondenceDetector.cs`

### Expected parent email file but not found
- The behavior has changed: **no separate parent email file is created**
- The parent correspondence is saved as `01_correspondence_...html` with `IsParent: true` metadata
- All correspondences are treated as individual files
