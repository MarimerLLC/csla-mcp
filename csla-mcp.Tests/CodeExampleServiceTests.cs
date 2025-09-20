using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using csla_mcp.Server;
using Xunit;

namespace csla_mcp.Tests;

public class CodeExampleServiceTests
{
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly Mock<ILogger<CodeExampleService>> _mockLogger;
    private readonly string _testExamplesPath;

    public CodeExampleServiceTests()
    {
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockLogger = new Mock<ILogger<CodeExampleService>>();
        
        // Create a temporary directory for test examples
        _testExamplesPath = Path.Combine(Path.GetTempPath(), "test-code-examples", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testExamplesPath);
        
        _mockEnvironment.Setup(x => x.ContentRootPath).Returns(Path.GetDirectoryName(_testExamplesPath)!);
    }

    [Fact]
    public async Task GetAvailableConcepts_ShouldReturnEmptyList_WhenNoExamplesExist()
    {
        // Arrange
        var service = new CodeExampleService(_mockEnvironment.Object, _mockLogger.Object);

        // Act
        var concepts = await service.GetAvailableConcepts();

        // Assert
        Assert.NotNull(concepts);
        // The service creates default examples, so we should have some concepts
        Assert.True(concepts.Count > 0);
    }

    [Fact]
    public async Task GetExamplesByConcept_ShouldReturnEmpty_WhenConceptDoesNotExist()
    {
        // Arrange
        var service = new CodeExampleService(_mockEnvironment.Object, _mockLogger.Object);

        // Act
        var examples = await service.GetExamplesByConcept("non-existent-concept");

        // Assert
        Assert.NotNull(examples);
        Assert.Empty(examples);
    }

    [Fact]
    public async Task SearchExamples_ShouldReturnMatchingExamples()
    {
        // Arrange
        var service = new CodeExampleService(_mockEnvironment.Object, _mockLogger.Object);
        
        // Create test examples
        await service.GetAvailableConcepts(); // This will create default examples

        // Act
        var examples = await service.SearchExamples("Customer");

        // Assert
        Assert.NotNull(examples);
        // Should find examples containing "Customer"
        Assert.Contains(examples, ex => 
            ex.Content.Contains("Customer", StringComparison.OrdinalIgnoreCase) ||
            ex.Name.Contains("Customer", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("business-object")]
    [InlineData("data-portal")]
    [InlineData("validation")]
    public async Task GetExamplesByConcept_ShouldReturnExamples_ForValidConcepts(string concept)
    {
        // Arrange
        var service = new CodeExampleService(_mockEnvironment.Object, _mockLogger.Object);
        
        // Ensure default examples are created
        await service.GetAvailableConcepts();

        // Act
        var examples = await service.GetExamplesByConcept(concept);

        // Assert
        Assert.NotNull(examples);
        Assert.True(examples.Count > 0);
        Assert.All(examples, ex => Assert.Equal(concept, ex.Concept));
    }

    [Fact]
    public async Task GetExamplesByConcept_WithCategory_ShouldFilterByCategory()
    {
        // Arrange
        var service = new CodeExampleService(_mockEnvironment.Object, _mockLogger.Object);
        
        // Ensure default examples are created
        await service.GetAvailableConcepts();

        // Act
        var examples = await service.GetExamplesByConcept("business-object", "basic");

        // Assert
        Assert.NotNull(examples);
        Assert.All(examples, ex => 
        {
            Assert.Equal("business-object", ex.Concept);
            Assert.Equal("basic", ex.Category);
        });
    }

    private void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testExamplesPath))
        {
            Directory.Delete(_testExamplesPath, true);
        }
    }
}