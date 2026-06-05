using Shouldly;

namespace Native.Mcp.Testing;

/// <summary>Shouldly-style assertion helpers for Native.Mcp result/response types.</summary>
public static class McpAssertionsExtensions
{
    /// <summary>Asserts the tool result is a success.</summary>
    /// <typeparam name="T">The result data type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The result, for chaining.</returns>
    public static McpToolResult<T> ShouldBeSuccess<T>(this McpToolResult<T> result)
        where T : class
    {
        result.IsSuccess.ShouldBeTrue(
            $"the tool result should be a success but was a failure: {result.Error?.Detail}");
        return result;
    }

    /// <summary>Asserts the tool result is a failure.</summary>
    /// <typeparam name="T">The result data type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The result, for chaining.</returns>
    public static McpToolResult<T> ShouldBeFailure<T>(this McpToolResult<T> result)
        where T : class
    {
        result.IsSuccess.ShouldBeFalse("the tool result should be a failure but was a success");
        return result;
    }

    /// <summary>Asserts the failure problem type equals <paramref name="problemType"/>.</summary>
    /// <typeparam name="T">The result data type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="problemType">The expected problem-type URI.</param>
    /// <returns>The result, for chaining.</returns>
    public static McpToolResult<T> ShouldHaveProblemType<T>(this McpToolResult<T> result, string problemType)
        where T : class
    {
        result.Error.ShouldNotBeNull("a failure result must carry an error");
        result.Error!.Type.ShouldBe(problemType);
        return result;
    }

    /// <summary>Asserts the call returned a successful canonical envelope.</summary>
    /// <param name="response">The tool-call response.</param>
    /// <returns>The response, for chaining.</returns>
    public static McpToolCallResponse ShouldBeSuccessful(this McpToolCallResponse response)
    {
        response.ProtocolError.ShouldBeNull("the call should not be a protocol error");
        response.Envelope.ShouldNotBeNull();
        response.IsError.ShouldBeFalse("isError should be false on a successful call");
        response.Envelope!.Success.ShouldBeTrue("the envelope should report success");
        return response;
    }

    /// <summary>Asserts the call returned an error envelope (HTTP 200, <c>isError=true</c>).</summary>
    /// <param name="response">The tool-call response.</param>
    /// <returns>The response, for chaining.</returns>
    public static McpToolCallResponse ShouldBeError(this McpToolCallResponse response)
    {
        response.ProtocolError.ShouldBeNull("a tool error is not a protocol error");
        response.Envelope.ShouldNotBeNull();
        response.IsError.ShouldBeTrue("isError should be true on an error call");
        response.Envelope!.Success.ShouldBeFalse("the envelope should report failure");
        return response;
    }

    /// <summary>Asserts the error envelope's problem type equals <paramref name="problemType"/>.</summary>
    /// <param name="response">The tool-call response.</param>
    /// <param name="problemType">The expected problem-type URI.</param>
    /// <returns>The response, for chaining.</returns>
    public static McpToolCallResponse ShouldHaveProblemType(this McpToolCallResponse response, string problemType)
    {
        response.Envelope.ShouldNotBeNull();
        response.Envelope!.Error.ShouldNotBeNull("an error envelope must carry a problem");
        response.Envelope!.Error!.Type.ShouldBe(problemType);
        return response;
    }

    /// <summary>Asserts the call failed at the JSON-RPC protocol layer with the given code.</summary>
    /// <param name="response">The tool-call response.</param>
    /// <param name="code">The expected JSON-RPC error code.</param>
    /// <returns>The response, for chaining.</returns>
    public static McpToolCallResponse ShouldBeProtocolError(this McpToolCallResponse response, int code)
    {
        response.ProtocolError.ShouldNotBeNull("the call should be a protocol error");
        response.ProtocolError!.Code.ShouldBe(code);
        return response;
    }
}
