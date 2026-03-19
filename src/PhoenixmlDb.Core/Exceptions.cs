using System;

namespace PhoenixmlDb.Core;

/// <summary>
/// Base exception for XmlDb errors.
/// </summary>
public class XmlDbException : Exception
{
    public XmlDbException(string message) : base(message) { }
    public XmlDbException(string message, Exception inner) : base(message, inner) { }

    public XmlDbException()
    {
    }
}

/// <summary>
/// Thrown when a container is not found.
/// </summary>
public class ContainerNotFoundException : XmlDbException
{
    public string ContainerName { get; }

    public ContainerNotFoundException(string containerName)
        : base($"Container '{containerName}' not found")
    {
        ContainerName = containerName;
    }

    public ContainerNotFoundException()
    {
        ContainerName = string.Empty;
    }

    public ContainerNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
        ContainerName = string.Empty;
    }
}

/// <summary>
/// Thrown when a document is not found.
/// </summary>
public class DocumentNotFoundException : XmlDbException
{
    public ContainerId Container { get; }
    public string DocumentName { get; }

    public DocumentNotFoundException(ContainerId container, string documentName)
        : base($"Document '{documentName}' not found in container {container}")
    {
        Container = container;
        DocumentName = documentName;
    }

    public DocumentNotFoundException(string documentName)
    {
        DocumentName = documentName;
        Container = ContainerId.None;
    }

    public DocumentNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
        DocumentName = string.Empty;
        Container = ContainerId.None;
    }

    public DocumentNotFoundException()
    {
        DocumentName = string.Empty;
        Container = ContainerId.None;
    }
}

/// <summary>
/// Thrown when a document already exists and overwrite is not allowed.
/// </summary>
public class DocumentExistsException : XmlDbException
{
    public ContainerId Container { get; }
    public string DocumentName { get; }

    public DocumentExistsException(ContainerId container, string documentName)
        : base($"Document '{documentName}' already exists in container {container}")
    {
        Container = container;
        DocumentName = documentName;
    }

    public DocumentExistsException()
    {
        Container = ContainerId.None;
        DocumentName = string.Empty;
    }

    public DocumentExistsException(string message, Exception innerException) : base(message, innerException)
    {
        Container = ContainerId.None;
        DocumentName = string.Empty;
    }

    public DocumentExistsException(string message) : base(message)
    {
        Container = ContainerId.None;
        DocumentName = string.Empty;
    }
}

/// <summary>
/// Thrown when a transaction operation fails.
/// </summary>
public class TransactionException : XmlDbException
{
    public TransactionException(string message) : base(message) { }
    public TransactionException(string message, Exception inner) : base(message, inner) { }

    public TransactionException() { }
}

/// <summary>
/// Thrown when a write transaction cannot be acquired within the timeout.
/// </summary>
public class TransactionTimeoutException : TransactionException
{
    public TimeSpan Timeout { get; }

    public TransactionTimeoutException(TimeSpan timeout)
        : base($"Could not acquire write transaction within {timeout}")
    {
        Timeout = timeout;
    }

    public TransactionTimeoutException()
    {
        Timeout = TimeSpan.Zero;
    }

    public TransactionTimeoutException(string message, Exception innerException) : base(message, innerException)
    {
        Timeout = TimeSpan.Zero;
    }

    public TransactionTimeoutException(string message) : base(message)
    {
        Timeout = TimeSpan.Zero;
    }
}

/// <summary>
/// Thrown when an XQuery parse or execution error occurs.
/// </summary>
public class XQueryException : XmlDbException
{
    public string? ErrorCode { get; }
    public int? Line { get; }
    public int? Column { get; }

    public XQueryException(string message, string? errorCode = null, int? line = null, int? column = null)
        : base(FormatMessage(message, errorCode, line, column))
    {
        ErrorCode = errorCode;
        Line = line;
        Column = column;
    }

    private static string FormatMessage(string message, string? errorCode, int? line, int? column)
    {
        var prefix = errorCode is not null ? $"[{errorCode}] " : "";
        var location = line.HasValue ? $" at line {line}, column {column}" : "";
        return $"{prefix}{message}{location}";
    }

    public XQueryException()
    {
    }

    public XQueryException(string message) : base(message)
    {
    }

    public XQueryException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when document content is malformed.
/// </summary>
public class DocumentParseException : XmlDbException
{
    public string DocumentName { get; }
    public int? Line { get; }
    public int? Column { get; }

    public DocumentParseException(string documentName, string message, int? line = null, int? column = null)
        : base(FormatMessage(documentName, message, line, column))
    {
        DocumentName = documentName;
        Line = line;
        Column = column;
    }

    public DocumentParseException(string documentName, string message, Exception inner)
        : base($"Failed to parse document '{documentName}': {message}", inner)
    {
        DocumentName = documentName;
    }

    private static string FormatMessage(string documentName, string message, int? line, int? column)
    {
        var location = line.HasValue ? $" at line {line}, column {column}" : "";
        return $"Failed to parse document '{documentName}'{location}: {message}";
    }

    public DocumentParseException()
    {
        DocumentName = string.Empty;
    }

    public DocumentParseException(string message) : base(message)
    {
        DocumentName = string.Empty;
    }

    public DocumentParseException(string message, Exception innerException) : base(message, innerException)
    {
        DocumentName = string.Empty;
    }
}
