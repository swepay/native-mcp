using Amazon.Lambda.APIGatewayEvents;
using Microsoft.Extensions.DependencyInjection;

namespace Native.Mcp.ApiGateway;

/// <summary>
/// Entry point that bridges an API Gateway HTTP API v2 Lambda invocation to the MCP
/// dispatcher. Create one (resolved from DI) and call <see cref="HandleAsync"/> from your
/// Lambda function handler. A fresh DI scope is created per invocation so scoped tool
/// dependencies behave correctly.
/// </summary>
public sealed class McpLambdaHandler
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Initializes a new <see cref="McpLambdaHandler"/>.</summary>
    /// <param name="serviceProvider">The root service provider.</param>
    public McpLambdaHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>Handles a single HTTP API v2 proxy request.</summary>
    /// <param name="request">The proxy request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The proxy response.</returns>
    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<McpJsonRpcDispatcher>();

        var adapted = ApiGatewayRequestAdapter.Adapt(request);
        var result = await dispatcher.DispatchAsync(adapted.Body, adapted.Context, cancellationToken)
            .ConfigureAwait(false);

        return ApiGatewayResponseBuilder.Build(result);
    }
}
