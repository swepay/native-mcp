using NativeLambdaRouter;
using NativeMediator;

namespace Native.Mcp.NativeLambdaRouter;

/// <summary>
/// A mediator that is never invoked. <see cref="McpRoutedApiGatewayFunction"/> dispatches MCP
/// requests through <c>Native.Mcp</c> (not the mediator), but the
/// <see cref="RoutedApiGatewayFunction"/> base requires an <see cref="IMediator"/>. Supplying
/// this no-op means consuming services do not have to register <c>NativeMediator</c> just to
/// host an MCP server.
/// </summary>
internal sealed class NoOpMediator : IMediator
{
    private const string Message =
        "This MCP function does not use the mediator; dispatch is handled by Native.Mcp.";

    /// <summary>Singleton instance.</summary>
    public static readonly NoOpMediator Instance = new();

    private NoOpMediator()
    {
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> Send<TResponse>(
        IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Message);

    /// <inheritdoc/>
    public ValueTask Publish<TNotification>(
        TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification =>
        throw new NotSupportedException(Message);

    /// <inheritdoc/>
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(Message);
}
