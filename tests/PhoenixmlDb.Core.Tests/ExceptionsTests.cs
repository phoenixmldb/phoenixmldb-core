using FluentAssertions;
using Xunit;

namespace PhoenixmlDb.Core.Tests;

/// <summary>
/// Tests for XmlDbException base exception type.
/// </summary>
public class XmlDbExceptionTests
{
    [Fact]
    public void DefaultConstructor_CreatesEmptyException()
    {
        var ex = new XmlDbException();

        ex.Message.Should().NotBeNull();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var message = "Test error message";
        var ex = new XmlDbException(message);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var message = "Test error message";
        var inner = new InvalidOperationException("Inner error");
        var ex = new XmlDbException(message, inner);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void IsExceptionType()
    {
        var ex = new XmlDbException("Test");
        ex.Should().BeAssignableTo<Exception>();
    }
}

/// <summary>
/// Tests for ContainerNotFoundException.
/// </summary>
public class ContainerNotFoundExceptionTests
{
    [Fact]
    public void DefaultConstructor_SetsEmptyContainerName()
    {
        var ex = new ContainerNotFoundException();

        ex.ContainerName.Should().BeEmpty();
    }

    [Fact]
    public void ContainerNameConstructor_SetsProperties()
    {
        var containerName = "test-container";
        var ex = new ContainerNotFoundException(containerName);

        ex.ContainerName.Should().Be(containerName);
        ex.Message.Should().Contain(containerName);
        ex.Message.Should().Contain("not found");
    }

    [Theory]
    [InlineData("my-container")]
    [InlineData("test")]
    [InlineData("container/with/path")]
    public void Message_FormatsCorrectly(string containerName)
    {
        var ex = new ContainerNotFoundException(containerName);

        ex.Message.Should().Be($"Container '{containerName}' not found");
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var message = "Custom message";
        var inner = new InvalidOperationException("Inner error");
        var ex = new ContainerNotFoundException(message, inner);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
        ex.ContainerName.Should().BeEmpty();
    }

    [Fact]
    public void InheritsFromXmlDbException()
    {
        var ex = new ContainerNotFoundException("test");
        ex.Should().BeAssignableTo<XmlDbException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("container-name")]
    public void AcceptsVariousContainerNames(string containerName)
    {
        var ex = new ContainerNotFoundException(containerName);
        ex.ContainerName.Should().Be(containerName);
    }
}

/// <summary>
/// Tests for DocumentNotFoundException.
/// </summary>
public class DocumentNotFoundExceptionTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var ex = new DocumentNotFoundException();

        ex.DocumentName.Should().BeEmpty();
        ex.Container.Should().Be(ContainerId.None);
    }

    [Fact]
    public void DocumentNameConstructor_SetsProperties()
    {
        var documentName = "test-document";
        var ex = new DocumentNotFoundException(documentName);

        ex.DocumentName.Should().Be(documentName);
        ex.Container.Should().Be(ContainerId.None);
    }

    [Fact]
    public void FullConstructor_SetsAllProperties()
    {
        var container = new ContainerId(42);
        var documentName = "test-document.xml";
        var ex = new DocumentNotFoundException(container, documentName);

        ex.Container.Should().Be(container);
        ex.DocumentName.Should().Be(documentName);
        ex.Message.Should().Contain(documentName);
        ex.Message.Should().Contain("42");
        ex.Message.Should().Contain("not found");
    }

    [Theory]
    [InlineData(1u, "doc1.xml")]
    [InlineData(100u, "path/to/doc.xml")]
    [InlineData(uint.MaxValue, "test")]
    public void Message_FormatsCorrectly(uint containerId, string documentName)
    {
        var container = new ContainerId(containerId);
        var ex = new DocumentNotFoundException(container, documentName);

        ex.Message.Should().Be($"Document '{documentName}' not found in container C:{containerId}");
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var message = "Custom message";
        var inner = new InvalidOperationException("Inner error");
        var ex = new DocumentNotFoundException(message, inner);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
        ex.DocumentName.Should().BeEmpty();
        ex.Container.Should().Be(ContainerId.None);
    }

    [Fact]
    public void InheritsFromXmlDbException()
    {
        var ex = new DocumentNotFoundException(new ContainerId(1), "test");
        ex.Should().BeAssignableTo<XmlDbException>();
    }
}

/// <summary>
/// Tests for DocumentExistsException.
/// </summary>
public class DocumentExistsExceptionTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var ex = new DocumentExistsException();

        ex.DocumentName.Should().BeEmpty();
        ex.Container.Should().Be(ContainerId.None);
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var message = "Custom message";
        var ex = new DocumentExistsException(message);

        ex.Message.Should().Be(message);
        ex.DocumentName.Should().BeEmpty();
        ex.Container.Should().Be(ContainerId.None);
    }

    [Fact]
    public void FullConstructor_SetsAllProperties()
    {
        var container = new ContainerId(42);
        var documentName = "test-document.xml";
        var ex = new DocumentExistsException(container, documentName);

        ex.Container.Should().Be(container);
        ex.DocumentName.Should().Be(documentName);
        ex.Message.Should().Contain(documentName);
        ex.Message.Should().Contain("already exists");
    }

    [Theory]
    [InlineData(1u, "doc1.xml")]
    [InlineData(100u, "path/to/doc.xml")]
    public void Message_FormatsCorrectly(uint containerId, string documentName)
    {
        var container = new ContainerId(containerId);
        var ex = new DocumentExistsException(container, documentName);

        ex.Message.Should().Be($"Document '{documentName}' already exists in container C:{containerId}");
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var message = "Custom message";
        var inner = new InvalidOperationException("Inner error");
        var ex = new DocumentExistsException(message, inner);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
        ex.DocumentName.Should().BeEmpty();
        ex.Container.Should().Be(ContainerId.None);
    }

    [Fact]
    public void InheritsFromXmlDbException()
    {
        var ex = new DocumentExistsException(new ContainerId(1), "test");
        ex.Should().BeAssignableTo<XmlDbException>();
    }
}

/// <summary>
/// Tests for TransactionException.
/// </summary>
public class TransactionExceptionTests
{
    [Fact]
    public void DefaultConstructor_CreatesException()
    {
        var ex = new TransactionException();

        ex.Message.Should().NotBeNull();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var message = "Transaction failed";
        var ex = new TransactionException(message);

        ex.Message.Should().Be(message);
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var message = "Transaction failed";
        var inner = new InvalidOperationException("Inner error");
        var ex = new TransactionException(message, inner);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void InheritsFromXmlDbException()
    {
        var ex = new TransactionException("Test");
        ex.Should().BeAssignableTo<XmlDbException>();
    }
}

/// <summary>
/// Tests for TransactionTimeoutException.
/// </summary>
public class TransactionTimeoutExceptionTests
{
    [Fact]
    public void DefaultConstructor_SetsZeroTimeout()
    {
        var ex = new TransactionTimeoutException();

        ex.Timeout.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TimeoutConstructor_SetsProperties()
    {
        var timeout = TimeSpan.FromSeconds(30);
        var ex = new TransactionTimeoutException(timeout);

        ex.Timeout.Should().Be(timeout);
        ex.Message.Should().Contain("30");
        ex.Message.Should().Contain("acquire");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(60000)]
    public void Timeout_DifferentValues(int milliseconds)
    {
        var timeout = TimeSpan.FromMilliseconds(milliseconds);
        var ex = new TransactionTimeoutException(timeout);

        ex.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var message = "Custom timeout message";
        var ex = new TransactionTimeoutException(message);

        ex.Message.Should().Be(message);
        ex.Timeout.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var message = "Custom timeout message";
        var inner = new InvalidOperationException("Inner error");
        var ex = new TransactionTimeoutException(message, inner);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
        ex.Timeout.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void InheritsFromTransactionException()
    {
        var ex = new TransactionTimeoutException(TimeSpan.FromSeconds(5));
        ex.Should().BeAssignableTo<TransactionException>();
    }

    [Fact]
    public void InheritsFromXmlDbException()
    {
        var ex = new TransactionTimeoutException(TimeSpan.FromSeconds(5));
        ex.Should().BeAssignableTo<XmlDbException>();
    }

    [Fact]
    public void Message_FormatsCorrectly()
    {
        var timeout = TimeSpan.FromSeconds(30);
        var ex = new TransactionTimeoutException(timeout);

        ex.Message.Should().Be($"Could not acquire write transaction within {timeout}");
    }
}

/// <summary>
/// Tests for XQueryException.
/// </summary>
public class XQueryExceptionTests
{
    [Fact]
    public void DefaultConstructor_CreatesException()
    {
        var ex = new XQueryException();

        ex.Message.Should().NotBeNull();
        ex.ErrorCode.Should().BeNull();
        ex.Line.Should().BeNull();
        ex.Column.Should().BeNull();
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var message = "Query failed";
        var ex = new XQueryException(message);

        ex.Message.Should().Be(message);
        ex.ErrorCode.Should().BeNull();
        ex.Line.Should().BeNull();
        ex.Column.Should().BeNull();
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var message = "Query failed";
        var inner = new InvalidOperationException("Inner error");
        var ex = new XQueryException(message, inner);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void FullConstructor_SetsAllProperties()
    {
        var message = "Syntax error";
        var errorCode = "XPST0003";
        var line = 10;
        var column = 25;

        var ex = new XQueryException(message, errorCode, line, column);

        ex.ErrorCode.Should().Be(errorCode);
        ex.Line.Should().Be(line);
        ex.Column.Should().Be(column);
        ex.Message.Should().Contain(errorCode);
        ex.Message.Should().Contain("10");
        ex.Message.Should().Contain("25");
    }

    [Theory]
    [InlineData("Error", null, null, null, "Error")]
    [InlineData("Error", "XPST0003", null, null, "[XPST0003] Error")]
    [InlineData("Error", null, 10, 5, "Error at line 10, column 5")]
    [InlineData("Error", "XPST0003", 10, 5, "[XPST0003] Error at line 10, column 5")]
    public void Message_FormatsCorrectly(string message, string? errorCode, int? line, int? column, string expected)
    {
        var ex = new XQueryException(message, errorCode, line, column);
        ex.Message.Should().Be(expected);
    }

    [Fact]
    public void MessageOnly_NoLocationInfo()
    {
        var ex = new XQueryException("Simple error", null, null, null);

        ex.Message.Should().Be("Simple error");
        ex.ErrorCode.Should().BeNull();
        ex.Line.Should().BeNull();
        ex.Column.Should().BeNull();
    }

    [Theory]
    [InlineData("XPST0003")]
    [InlineData("XQST0004")]
    [InlineData("FOER0000")]
    [InlineData("CUSTOM001")]
    public void AcceptsVariousErrorCodes(string errorCode)
    {
        var ex = new XQueryException("Error", errorCode);

        ex.ErrorCode.Should().Be(errorCode);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(1000, 1)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void AcceptsVariousLineColumnValues(int line, int column)
    {
        var ex = new XQueryException("Error", null, line, column);

        ex.Line.Should().Be(line);
        ex.Column.Should().Be(column);
    }

    [Fact]
    public void InheritsFromXmlDbException()
    {
        var ex = new XQueryException("Test");
        ex.Should().BeAssignableTo<XmlDbException>();
    }
}

/// <summary>
/// Tests for DocumentParseException.
/// </summary>
public class DocumentParseExceptionTests
{
    [Fact]
    public void DefaultConstructor_SetsEmptyDocumentName()
    {
        var ex = new DocumentParseException();

        ex.DocumentName.Should().BeEmpty();
        ex.Line.Should().BeNull();
        ex.Column.Should().BeNull();
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        var message = "Parse error";
        var ex = new DocumentParseException(message);

        ex.Message.Should().Be(message);
        ex.DocumentName.Should().BeEmpty();
    }

    [Fact]
    public void MessageAndInnerConstructor_SetsBoth()
    {
        var message = "Parse error";
        var inner = new InvalidOperationException("Inner error");
        var ex = new DocumentParseException(message, inner);

        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
        ex.DocumentName.Should().BeEmpty();
    }

    [Fact]
    public void FullConstructor_SetsAllProperties()
    {
        var documentName = "test.xml";
        var message = "Invalid element";
        var line = 5;
        var column = 10;

        var ex = new DocumentParseException(documentName, message, line, column);

        ex.DocumentName.Should().Be(documentName);
        ex.Line.Should().Be(line);
        ex.Column.Should().Be(column);
        ex.Message.Should().Contain(documentName);
        ex.Message.Should().Contain("5");
        ex.Message.Should().Contain("10");
    }

    [Theory]
    [InlineData("doc.xml", "Error", null, null, "Failed to parse document 'doc.xml': Error")]
    [InlineData("doc.xml", "Error", 5, 10, "Failed to parse document 'doc.xml' at line 5, column 10: Error")]
    [InlineData("path/to/doc.xml", "Invalid", 100, 200, "Failed to parse document 'path/to/doc.xml' at line 100, column 200: Invalid")]
    public void Message_FormatsCorrectly(string docName, string message, int? line, int? column, string expected)
    {
        var ex = new DocumentParseException(docName, message, line, column);
        ex.Message.Should().Be(expected);
    }

    [Fact]
    public void InnerExceptionConstructor_FormatsMessage()
    {
        var inner = new InvalidOperationException("Inner error");
        var ex = new DocumentParseException("test.xml", "Parse failed", inner);

        ex.DocumentName.Should().Be("test.xml");
        ex.InnerException.Should().BeSameAs(inner);
        ex.Message.Should().Be("Failed to parse document 'test.xml': Parse failed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("doc.xml")]
    [InlineData("path/to/document.xml")]
    [InlineData("document with spaces.xml")]
    public void AcceptsVariousDocumentNames(string documentName)
    {
        var ex = new DocumentParseException(documentName, "Error");
        ex.DocumentName.Should().Be(documentName);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(1000, 1)]
    public void AcceptsVariousLineColumnValues(int line, int column)
    {
        var ex = new DocumentParseException("doc.xml", "Error", line, column);

        ex.Line.Should().Be(line);
        ex.Column.Should().Be(column);
    }

    [Fact]
    public void InheritsFromXmlDbException()
    {
        var ex = new DocumentParseException("doc.xml", "Error");
        ex.Should().BeAssignableTo<XmlDbException>();
    }

    [Fact]
    public void WithNullLineColumn_OmitsLocationFromMessage()
    {
        var ex = new DocumentParseException("doc.xml", "Error", null, null);

        ex.Line.Should().BeNull();
        ex.Column.Should().BeNull();
        ex.Message.Should().NotContain("at line");
    }
}
