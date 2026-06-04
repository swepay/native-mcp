using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Native.FluentValidation.Core;
using Native.Mcp;

namespace Native.Mcp.Tests.Fixtures;

/// <summary>Input for the echo tool.</summary>
public sealed record EchoInput
{
    /// <summary>The message to echo back.</summary>
    [Description("The message to echo back.")]
    [MaxLength(100)]
    public required string Message { get; init; }
}

/// <summary>Output of the echo tool.</summary>
public sealed record EchoOutput
{
    /// <summary>The echoed message.</summary>
    public required string Echo { get; init; }
}

/// <summary>Echoes the input message back to the caller.</summary>
public sealed partial class EchoTool : IMcpTool<EchoInput, EchoOutput>
{
    /// <inheritdoc/>
    public static string Name => "echo";

    /// <inheritdoc/>
    public static string Description => "Echoes the provided message. Do not use for binary payloads.";

    /// <inheritdoc/>
    public Task<McpToolResult<EchoOutput>> ExecuteAsync(
        EchoInput input,
        McpExecutionContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(McpToolResult<EchoOutput>.Success(new EchoOutput { Echo = input.Message }));
}

/// <summary>Validator requiring a non-empty message of at most 100 chars.</summary>
public sealed class EchoInputValidator : NativeValidator<EchoInput>
{
    /// <summary>Initializes the rules.</summary>
    public EchoInputValidator()
    {
        RuleFor(x => x.Message, "message").NotEmpty().Length(1, 100);
    }
}
