using System;

namespace PhoenixmlDb.Core;

/// <summary>
/// Base exception for all PhoenixmlDb errors. Catch this type to handle any database-related
/// error in a single handler.
/// </summary>
/// <remarks>
/// <para>
/// All PhoenixmlDb exceptions derive from <c>XmlDbException</c>, making it the broadest
/// catch target for database operations. In production code, prefer catching the specific
/// derived exceptions (e.g., <see cref="ContainerNotFoundException"/>,
/// <see cref="XQueryException"/>) to handle each failure mode appropriately, and use
/// <c>XmlDbException</c> only as a fallback.
/// </para>
/// </remarks>
/// <seealso cref="ContainerNotFoundException"/>
/// <seealso cref="DocumentNotFoundException"/>
/// <seealso cref="DocumentExistsException"/>
/// <seealso cref="TransactionException"/>
/// <seealso cref="XQueryException"/>
/// <seealso cref="DocumentParseException"/>
public class XmlDbException : Exception
{
    public XmlDbException(string message) : base(message) { }
    public XmlDbException(string message, Exception inner) : base(message, inner) { }

    public XmlDbException()
    {
    }
}

/// <summary>
/// Thrown when a container with the specified name does not exist in the database.
/// </summary>
/// <remarks>
/// <para>
/// <b>When it's thrown:</b> By <see cref="IDocumentDatabase.OpenContainerAsync"/> (which returns
/// <c>null</c> instead of throwing — this exception is thrown by internal operations that
/// require a container to exist) and by operations that reference a container by name that
/// has been deleted or never created.
/// </para>
/// <para>
/// <b>How to handle it:</b> Check whether the container name is correct. If the container
/// may not exist yet, use <see cref="IDocumentDatabase.OpenOrCreateContainerAsync"/> which
/// creates the container on first access. Alternatively, call
/// <see cref="IDocumentDatabase.OpenContainerAsync"/> and check for <c>null</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     var container = await db.OpenOrCreateContainerAsync("products");
/// }
/// catch (ContainerNotFoundException ex)
/// {
///     Console.WriteLine($"Container not found: {ex.ContainerName}");
/// }
/// </code>
/// </example>
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
/// Thrown when a document with the specified name does not exist in the container.
/// </summary>
/// <remarks>
/// <para>
/// <b>When it's thrown:</b> By operations that require a document to exist, such as
/// <see cref="IContainer.SetMetadataAsync"/> or <see cref="IWriteTransaction.SetMetadataAsync"/>
/// when the target document has not been stored. Note that
/// <see cref="IContainer.GetDocumentAsync"/> returns <c>null</c> rather than throwing.
/// </para>
/// <para>
/// <b>How to handle it:</b> Check the <see cref="DocumentName"/> property for the missing
/// document's name and the <see cref="Container"/> property for the container it was
/// expected in. Use <see cref="IContainer.DocumentExistsAsync"/> for a lightweight
/// existence check before operations that require the document.
/// </para>
/// </remarks>
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
/// Thrown when attempting to store a document with a name that already exists and
/// <see cref="DocumentOptions.Overwrite"/> is set to <c>false</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>When it's thrown:</b> By
/// <see cref="IContainer.PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>
/// and <see cref="IWriteTransaction.PutDocumentAsync"/> when the document name is already
/// taken and the <see cref="DocumentOptions.Overwrite"/> option is explicitly set to <c>false</c>.
/// With default options (overwrite enabled), this exception is never thrown.
/// </para>
/// <para>
/// <b>How to handle it:</b> This is the expected exception for insert-only workflows. Catch
/// it to detect duplicate inserts and either skip the duplicate, generate a unique name, or
/// report the conflict to the caller.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await container.PutDocumentAsync("events/evt-42.json", json,
///         new DocumentOptions { Overwrite = false });
/// }
/// catch (DocumentExistsException ex)
/// {
///     Console.WriteLine($"Duplicate: {ex.DocumentName} already exists in {ex.Container}");
/// }
/// </code>
/// </example>
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
/// Thrown when a transaction operation fails due to an invalid state or internal error.
/// </summary>
/// <remarks>
/// <para>
/// <b>When it's thrown:</b> When attempting to commit or roll back a transaction that has
/// already been committed, rolled back, or disposed. Also thrown for internal LMDB
/// transaction errors that do not fit a more specific exception type.
/// </para>
/// <para>
/// <b>How to handle it:</b> This usually indicates a programming error (e.g., using a
/// transaction after disposal). Review the transaction lifecycle in your code to ensure
/// each transaction is committed or rolled back exactly once.
/// </para>
/// </remarks>
/// <seealso cref="TransactionTimeoutException"/>
/// <seealso cref="IWriteTransaction"/>
public class TransactionException : XmlDbException
{
    public TransactionException(string message) : base(message) { }
    public TransactionException(string message, Exception inner) : base(message, inner) { }

    public TransactionException() { }
}

/// <summary>
/// Thrown when a write transaction cannot be acquired within the specified timeout period.
/// </summary>
/// <remarks>
/// <para>
/// <b>When it's thrown:</b> By
/// <see cref="IDocumentDatabase.BeginWriteAsync(TimeSpan, CancellationToken)"/> when another
/// write transaction is in progress and does not complete before the timeout expires.
/// </para>
/// <para>
/// <b>How to handle it:</b> This is a contention signal — the database is busy with another
/// write. Common strategies include:
/// </para>
/// <list type="bullet">
/// <item><description><b>Retry with backoff:</b> Wait briefly and try again, especially for batch operations that can tolerate short delays.</description></item>
/// <item><description><b>Increase the timeout:</b> If the workload involves large transactions, a longer timeout may be appropriate.</description></item>
/// <item><description><b>Reduce write contention:</b> Break large transactions into smaller batches to reduce lock hold time.</description></item>
/// </list>
/// <para>
/// The <see cref="Timeout"/> property indicates the timeout that was exceeded.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await using var txn = await db.BeginWriteAsync(TimeSpan.FromSeconds(5));
///     // ... perform writes ...
///     await txn.CommitAsync();
/// }
/// catch (TransactionTimeoutException ex)
/// {
///     Console.WriteLine($"Write lock not acquired within {ex.Timeout}. Retrying...");
///     // Implement retry logic here
/// }
/// </code>
/// </example>
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
/// Thrown when an XQuery expression fails to parse or encounters a runtime error during execution.
/// </summary>
/// <remarks>
/// <para>
/// <b>When it's thrown:</b> By <see cref="IContainer.QueryAsync"/> and
/// <see cref="IReadTransaction.QueryAsync"/> when the XQuery expression has a syntax error,
/// a type error, a dynamic error (e.g., division by zero), or references an undeclared
/// variable or function.
/// </para>
/// <para>
/// <b>Debugging information:</b> This exception provides structured error details:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="ErrorCode"/> — the W3C-defined error code (e.g., <c>"XPST0003"</c> for syntax errors, <c>"XPDY0002"</c> for dynamic errors, <c>"FOAR0002"</c> for division by zero). See the XQuery specification for the full error code catalog.</description></item>
/// <item><description><see cref="Line"/> — the line number in the query where the error was detected (1-based).</description></item>
/// <item><description><see cref="Column"/> — the column number within that line (1-based).</description></item>
/// </list>
/// <para>
/// <b>How to handle it:</b> Log the full exception message (which includes the error code
/// and location). For user-facing query interfaces, display the error code and location to
/// help users fix their queries. For application-embedded queries, this usually indicates a
/// bug in the query string.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await foreach (var result in container.QueryAsync("//product[price >"))
///     {
///         Console.WriteLine(result);
///     }
/// }
/// catch (XQueryException ex)
/// {
///     Console.WriteLine($"Query error [{ex.ErrorCode}] at line {ex.Line}, column {ex.Column}");
///     Console.WriteLine(ex.Message);
/// }
/// </code>
/// </example>
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
/// Thrown when document content cannot be parsed as well-formed XML or valid JSON.
/// </summary>
/// <remarks>
/// <para>
/// <b>When it's thrown:</b> By
/// <see cref="IContainer.PutDocumentAsync(string, string, DocumentOptions?, CancellationToken)"/>
/// and <see cref="IWriteTransaction.PutDocumentAsync"/> when the provided content fails to
/// parse. For XML, this means the content is not well-formed (e.g., mismatched tags, invalid
/// characters). For JSON, this means the content is not syntactically valid.
/// </para>
/// <para>
/// <b>Debugging information:</b> The <see cref="DocumentName"/> property identifies which
/// document failed to parse. The <see cref="Line"/> and <see cref="Column"/> properties
/// (when available) indicate the location of the parse error within the content.
/// </para>
/// <para>
/// <b>How to handle it:</b> This exception typically indicates bad input data. Validate
/// content before storing it, or catch this exception to report the error back to the data
/// source. For batch imports, catch per-document and log the failures rather than aborting
/// the entire import.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await container.PutDocumentAsync("data.xml", malformedContent);
/// }
/// catch (DocumentParseException ex)
/// {
///     Console.WriteLine($"Parse error in '{ex.DocumentName}' at line {ex.Line}, column {ex.Column}");
///     Console.WriteLine(ex.Message);
/// }
/// </code>
/// </example>
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
