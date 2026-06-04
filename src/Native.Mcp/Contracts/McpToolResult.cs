using System.Diagnostics.CodeAnalysis;

namespace Native.Mcp;

/// <summary>
/// The result of executing an <see cref="IMcpTool{TInput, TOutput}"/>. Either a
/// success carrying <typeparamref name="T"/> data, or a failure carrying a
/// <see cref="SwepayProblemDetails"/>. Construction is exhaustive: exactly one of
/// <see cref="Data"/> / <see cref="Error"/> is non-null.
/// </summary>
/// <typeparam name="T">The output data type.</typeparam>
public sealed class McpToolResult<T>
    where T : class
{
    private McpToolResult(bool isSuccess, T? data, SwepayProblemDetails? error)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
    }

    /// <summary>Whether the tool succeeded.</summary>
    [MemberNotNullWhen(true, nameof(Data))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>The success payload, or <see langword="null"/> on failure.</summary>
    public T? Data { get; }

    /// <summary>The error payload, or <see langword="null"/> on success.</summary>
    public SwepayProblemDetails? Error { get; }

    /// <summary>Creates a successful result.</summary>
    /// <param name="data">The non-null success payload.</param>
    /// <returns>A successful <see cref="McpToolResult{T}"/>.</returns>
    public static McpToolResult<T> Success(T data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new McpToolResult<T>(isSuccess: true, data: data, error: null);
    }

    /// <summary>Creates a failed result from a fully-populated problem-details payload.</summary>
    /// <param name="error">The non-null canonical error.</param>
    /// <returns>A failed <see cref="McpToolResult{T}"/>.</returns>
    public static McpToolResult<T> Failure(SwepayProblemDetails error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new McpToolResult<T>(isSuccess: false, data: null, error: error);
    }
}
