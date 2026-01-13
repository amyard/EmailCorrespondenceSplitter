using EmailCorrespondenceSplitter.Services;
using System.Text;

namespace EmailCorrespondenceSplitter.Tests;

/// <summary>
/// Tests for email correspondence extraction functionality.
/// These tests verify that emails are correctly parsed and split into individual correspondences.
/// All correspondences (including the parent) are saved as individual files.
/// </summary>
public class CorrespondenceExtractionTests
{
    // Register encoding provider once for all tests
    // This is required for MSG file parsing
    static CorrespondenceExtractionTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Test that email files are parsed and the expected number of correspondences are extracted.
    /// Uses Theory with InlineData to test multiple email files with their expected correspondence counts.
    /// </summary>
    /// <param name="emailPath">Path to the email file (relative to test output directory)</param>
    /// <param name="expectedCount">Expected number of correspondences to be extracted</param>
    [Theory]
    [InlineData("Assets/em1.msg", 2)] // outlook with 2 correspondences
    [InlineData("Assets/em2.msg", 2)] // outlook with 2 correspondences 
    [InlineData("Assets/em3.msg", 8)] // outlook with 8 correspondences and images
    [InlineData("Assets/em4.msg", 24)] // outlook with 24 forwarded correspondences
    [InlineData("Assets/em5.msg", 2)] // apple with 2 correspondences
    [InlineData("Assets/em6.msg", 4)] // outlook with 3 correspondences
    public async Task ProcessEmail_ShouldExtractExpectedCorrespondenceCount(string emailPath, int expectedCount)
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        
        // Act
        var email = await emailParser.ParseAsync(emailPath);
        var correspondences = correspondenceDetector.DetectCorrespondences(email);
        
        // Assert
        Assert.Equal(expectedCount, correspondences.Count);
    }

    /// <summary>
    /// Test the complete EmailSplitter workflow to verify it returns the correct correspondence count.
    /// This test uses the full EmailSplitter service which includes parsing, detection, and output management.
    /// All correspondences (including parent) are saved as individual files.
    /// </summary>
    /// <param name="emailPath">Path to the email file (relative to test output directory)</param>
    /// <param name="expectedCount">Expected number of correspondences to be extracted</param>
    [Theory]
    [InlineData("Assets/em1.msg", 2)]
    [InlineData("Assets/em2.msg", 2)]
    [InlineData("Assets/em3.msg", 8)]
    [InlineData("Assets/em4.msg", 24)]
    [InlineData("Assets/em5.msg", 2)]
    [InlineData("Assets/em6.msg", 4)]
    public async Task ProcessEmailWithSplitter_ShouldReturnExpectedCount(string emailPath, int expectedCount)
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var outputManager = new OutputManager("TestOutput");
        var emailSplitter = new EmailSplitter(emailParser, correspondenceDetector, outputManager);

        // Act
        var actualCount = await emailSplitter.ProcessEmailAsync(emailPath);

        // Assert
        Assert.Equal(expectedCount, actualCount);
    }

    /// <summary>
    /// Test that processing an email creates the expected number of individual correspondence files.
    /// No separate parent email file should be created.
    /// </summary>
    /// <param name="emailPath">Path to the email file</param>
    /// <param name="expectedCount">Expected number of correspondence files</param>
    [Theory]
    [InlineData("Assets/em1.msg", 2)]
    [InlineData("Assets/em2.msg", 2)]
    public async Task ProcessEmailWithSplitter_ShouldCreateIndividualCorrespondenceFiles(string emailPath, int expectedCount)
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testOutputFolder = $"TestOutput_{Guid.NewGuid():N}";
        var outputManager = new OutputManager(testOutputFolder);
        var emailSplitter = new EmailSplitter(emailParser, correspondenceDetector, outputManager);

        try
        {
            // Act
            await emailSplitter.ProcessEmailAsync(emailPath);

            // Assert - Check that the output folder contains the expected number of HTML files
            var outputFolders = Directory.GetDirectories(testOutputFolder);
            Assert.Single(outputFolders); // Should have one folder for the email

            var htmlFiles = Directory.GetFiles(outputFolders[0], "*.html");
            Assert.Equal(expectedCount, htmlFiles.Length);
            
            // Verify no parent email file exists
            Assert.DoesNotContain(htmlFiles, f => Path.GetFileName(f).Contains("parent"));
            
            // Verify all files are correspondence files
            Assert.All(htmlFiles, file => 
                Assert.Contains("correspondence", Path.GetFileName(file).ToLower()));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testOutputFolder))
            {
                Directory.Delete(testOutputFolder, true);
            }
        }
    }

    /// <summary>
    /// Test that processing an email creates the expected number of individual correspondence MSG files.
    /// No separate parent email file should be created.
    /// </summary>
    /// <param name="emailPath">Path to the email file</param>
    /// <param name="expectedCount">Expected number of correspondence files</param>
    [Theory]
    [InlineData("Assets/em1.msg", 2)]
    [InlineData("Assets/em2.msg", 2)]
    [InlineData("Assets/em6.msg", 4)]
    public async Task ProcessEmailWithSplitter_ShouldCreateIndividualCorrespondenceMsgFiles(string emailPath, int expectedCount)
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testOutputFolder = $"TestOutput_{Guid.NewGuid():N}";
        var outputManager = new OutputManager(testOutputFolder);
        var emailSplitter = new EmailSplitter(emailParser, correspondenceDetector, outputManager);

        try
        {
            // Act
            await emailSplitter.ProcessEmailAsync(emailPath);

            // Assert - Check that the output folder contains the expected number of MSG files
            var outputFolders = Directory.GetDirectories(testOutputFolder);
            Assert.Single(outputFolders); // Should have one folder for the email

            var msgFiles = Directory.GetFiles(outputFolders[0], "*.msg");
            Assert.Equal(expectedCount, msgFiles.Length);
            
            // Verify no parent email file exists
            Assert.DoesNotContain(msgFiles, f => Path.GetFileName(f).Contains("parent"));
            
            // Verify all files are correspondence MSG files
            Assert.All(msgFiles, file => 
                Assert.Contains("correspondence", Path.GetFileName(file).ToLower()));
            
            // Verify files are numbered sequentially
            for (int i = 0; i < msgFiles.Length; i++)
            {
                var fileName = Path.GetFileName(msgFiles.OrderBy(f => f).ToArray()[i]);
                Assert.StartsWith($"{i + 1:D2}_", fileName);
            }
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testOutputFolder))
            {
                Directory.Delete(testOutputFolder, true);
            }
        }
    }

    /// <summary>
    /// Test that extracted correspondence contains correct metadata.
    /// Verifies From, Subject, IsParent flag, and Index are properly set.
    /// </summary>
    [Fact]
    public async Task ProcessEmail_ShouldExtractCorrectMetadata()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testEmailPath = "Assets/em1.msg";

        // Act
        var email = await emailParser.ParseAsync(testEmailPath);
        var correspondences = correspondenceDetector.DetectCorrespondences(email);

        // Assert
        Assert.NotEmpty(correspondences);
        var firstCorrespondence = correspondences[0];
        Assert.NotNull(firstCorrespondence.From);
        Assert.NotNull(firstCorrespondence.Subject);
        Assert.True(firstCorrespondence.IsParent);
        Assert.Equal(0, firstCorrespondence.Index);
    }

    /// <summary>
    /// Test that parsing an invalid file path throws the expected exception.
    /// </summary>
    [Fact]
    public async Task ProcessEmail_WithInvalidPath_ShouldThrowException()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var invalidPath = "nonexistent/path/email.msg";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () => 
            await emailParser.ParseAsync(invalidPath));
    }

    /// <summary>
    /// Test that the parser correctly identifies MSG files as parseable.
    /// </summary>
    [Fact]
    public void CanParse_WithMsgFile_ShouldReturnTrue()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var msgFilePath = "test.msg";

        // Act
        var result = emailParser.CanParse(msgFilePath);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Test that the parser correctly rejects non-Msg files.
    /// </summary>
    /// <param name="filePath">Path to a non-MSG file</param>
    [Theory]
    [InlineData("test.eml")]
    [InlineData("test.txt")]
    [InlineData("test.pdf")]
    public void CanParse_WithNonMsgFile_ShouldReturnFalse(string filePath)
    {
        // Arrange
        var emailParser = new MsgEmailParser();

        // Act
        var result = emailParser.CanParse(filePath);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Test that all correspondences have required fields populated.
    /// Ensures no correspondence is missing critical data.
    /// </summary>
    [Theory]
    [InlineData("Assets/em1.msg")]
    [InlineData("Assets/em2.msg")]
    public async Task ProcessEmail_AllCorrespondencesShouldHaveRequiredFields(string emailPath)
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();

        // Act
        var email = await emailParser.ParseAsync(emailPath);
        var correspondences = correspondenceDetector.DetectCorrespondences(email);

        // Assert
        Assert.All(correspondences, correspondence =>
        {
            Assert.NotNull(correspondence.From);
            Assert.NotEmpty(correspondence.From);
            Assert.NotNull(correspondence.Subject);
            Assert.True(correspondence.Index >= 0);
        });
    }

    /// <summary>
    /// Test that the first correspondence is always marked as the parent.
    /// Even though parent and all correspondences are saved as individual files,
    /// the parent flag helps identify the original email.
    /// </summary>
    [Theory]
    [InlineData("Assets/em1.msg")]
    [InlineData("Assets/em2.msg")]
    [InlineData("Assets/em3.msg")]
    public async Task ProcessEmail_FirstCorrespondenceShouldBeParent(string emailPath)
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();

        // Act
        var email = await emailParser.ParseAsync(emailPath);
        var correspondences = correspondenceDetector.DetectCorrespondences(email);

        // Assert
        Assert.NotEmpty(correspondences);
        var parentCorrespondence = correspondences.FirstOrDefault(c => c.IsParent);
        Assert.NotNull(parentCorrespondence);
        Assert.Equal(0, parentCorrespondence.Index);
    }
    
    /// <summary>
    /// Test that each correspondence is saved with proper indexing (1-based in filename).
    /// </summary>
    [Fact]
    public async Task ProcessEmail_CorrespondencesShouldBeNumberedSequentially()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testOutputFolder = $"TestOutput_{Guid.NewGuid():N}";
        var outputManager = new OutputManager(testOutputFolder);
        var emailSplitter = new EmailSplitter(emailParser, correspondenceDetector, outputManager);
        var testEmailPath = "Assets/em1.msg";

        try
        {
            // Act
            await emailSplitter.ProcessEmailAsync(testEmailPath);

            // Assert
            var outputFolders = Directory.GetDirectories(testOutputFolder);
            var msgFiles = Directory.GetFiles(outputFolders[0], "*.msg").OrderBy(f => f).ToArray();
            
            for (int i = 0; i < msgFiles.Length; i++)
            {
                var fileName = Path.GetFileName(msgFiles[i]);
                // Should start with "01_", "02_", etc.
                Assert.StartsWith($"{i + 1:D2}_", fileName);
            }
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testOutputFolder))
            {
                Directory.Delete(testOutputFolder, true);
            }
        }
    }

    /// <summary>
    /// Test that Apple Mail correspondences are properly separated without nested content
    /// </summary>
    [Fact]
    public async Task ProcessEmail_Em5AppleMail_CorrespondencesShouldNotContainNestedContent()
    {
        // Arrange
        var emailParser = new MsgEmailParser();
        var correspondenceDetector = new CorrespondenceDetector();
        var testEmailPath = "Assets/em5.msg";

        // Act
        var email = await emailParser.ParseAsync(testEmailPath);
        var correspondences = correspondenceDetector.DetectCorrespondences(email);

        // Assert
        Assert.Equal(2, correspondences.Count);
        
        // The first correspondence should NOT contain content from the second correspondence
        // Check that the first correspondence doesn't contain the "From: Jack Lawrence" text
        var firstCorrespondenceText = correspondences[0].TextContent;
        var secondCorrespondenceText = correspondences[1].TextContent;
        
        // The second correspondence should have "From: Jack Lawrence" or similar
        Assert.Contains("Jack", secondCorrespondenceText, StringComparison.OrdinalIgnoreCase);
        
        // The first correspondence should NOT contain the full second correspondence
        // If it does, it means we haven't properly separated them
        // We check that the second correspondence has unique content not in the first
        var secondCorrespondenceLines = secondCorrespondenceText.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        
        // At least some lines from second correspondence should not appear in first
        var uniqueToSecond = secondCorrespondenceLines
            .Where(line => !firstCorrespondenceText.Contains(line, StringComparison.OrdinalIgnoreCase))
            .Count();
        
        // Expect that at least 30% of the second correspondence lines are unique
        // (not contained in the first correspondence)
        var percentageUnique = (double)uniqueToSecond / secondCorrespondenceLines.Count;
        Assert.True(percentageUnique > 0.3, 
            $"First correspondence appears to contain nested content from second correspondence. " +
            $"Only {percentageUnique:P0} of second correspondence lines are unique.");
    }
}
