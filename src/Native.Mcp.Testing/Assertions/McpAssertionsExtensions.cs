using FluentAssertions;

namespace Native.Mcp.Testing;

/// <summary>FluentAssertions entry points for Native.Mcp result/response types.</summary>
public static class McpAssertionsExtensions
{
    /// <summary>Begins an assertion chain on an <see cref="McpToolResult{T}"/>.</summary>
    /// <typeparam name="T">The result data type.</typeparam>
    /// <param name="instance">The result.</param>
    /// <returns>The assertions object.</returns>
    public static McpToolResultAssertions<T> Should<T>(this McpToolResult<T> instance)
        where T : class => new(instance);

    /// <summary>Begins an assertion chain on an <see cref="McpToolCallResponse"/>.</summary>
    /// <param name="instance">The response.</param>
    /// <returns>The assertions object.</returns>
    public static McpToolCallResponseAssertions Should(this McpToolCallResponse instance) => new(instance);
}

/// <summary>Assertions over <see cref="McpToolResult{T}"/>.</summary>
/// <typeparam name="T">The result data type.</typeparam>
public sealed class McpToolResultAssertions<T>
    where T : class
{
    private readonly McpToolResult<T> _subject;

    internal McpToolResultAssertions(McpToolResult<T> subject) => _subject = subject;

    /// <summary>Asserts the result is a success.</summary>
    /// <returns>This assertions object.</returns>
    public McpToolResultAssertions<T> BeSuccess()
    {
        _subject.IsSuccess.Should().BeTrue(
            "the tool result should be a success but was a failure: {0}", _subject.Error?.Detail);
        return this;
    }

    /// <summary>Asserts the result is a failure.</summary>
    /// <returns>This assertions object.</returns>
    public McpToolResultAssertions<T> BeFailure()
    {
        _subject.IsSuccess.Should().BeFalse("the tool result should be a failure but was a success");
        return this;
    }

    /// <summary>Asserts the failure problem type equals <paramref name="problemType"/>.</summary>
    /// <param name="problemType">The expected problem-type URI.</param>
    /// <returns>This assertions object.</returns>
    public McpToolResultAssertions<T> WithProblemType(string problemType)
    {
        _subject.Error.Should().NotBeNull("a failure result must carry an error");
        _subject.Error!.Type.Should().Be(problemType);
        return this;
    }
}

/// <summary>Assertions over <see cref="McpToolCallResponse"/>.</summary>
public sealed class McpToolCallResponseAssertions
{
    private readonly McpToolCallResponse _subject;

    internal McpToolCallResponseAssertions(McpToolCallResponse subject) => _subject = subject;

    /// <summary>Asserts the call returned a successful canonical envelope.</summary>
    /// <returns>This assertions object.</returns>
    public McpToolCallResponseAssertions BeSuccessful()
    {
        _subject.ProtocolError.Should().BeNull("the call should not be a protocol error");
        _subject.Envelope.Should().NotBeNull();
        _subject.IsError.Should().BeFalse("isError should be false on a successful call");
        _subject.Envelope!.Success.Should().BeTrue("the envelope should report success");
        return this;
    }

    /// <summary>Asserts the call returned an error envelope (HTTP 200, <c>isError=true</c>).</summary>
    /// <returns>This assertions object.</returns>
    public McpToolCallResponseAssertions BeError()
    {
        _subject.ProtocolError.Should().BeNull("a tool error is not a protocol error");
        _subject.Envelope.Should().NotBeNull();
        _subject.IsError.Should().BeTrue("isError should be true on an error call");
        _subject.Envelope!.Success.Should().BeFalse("the envelope should report failure");
        return this;
    }

    /// <summary>Asserts the error envelope's problem type equals <paramref name="problemType"/>.</summary>
    /// <param name="problemType">The expected problem-type URI.</param>
    /// <returns>This assertions object.</returns>
    public McpToolCallResponseAssertions WithProblemType(string problemType)
    {
        _subject.Envelope.Should().NotBeNull();
        _subject.Envelope!.Error.Should().NotBeNull("an error envelope must carry a problem");
        _subject.Envelope!.Error!.Type.Should().Be(problemType);
        return this;
    }

    /// <summary>Asserts the call failed at the JSON-RPC protocol layer with the given code.</summary>
    /// <param name="code">The expected JSON-RPC error code.</param>
    /// <returns>This assertions object.</returns>
    public McpToolCallResponseAssertions BeProtocolError(int code)
    {
        _subject.ProtocolError.Should().NotBeNull("the call should be a protocol error");
        _subject.ProtocolError!.Code.Should().Be(code);
        return this;
    }
}
