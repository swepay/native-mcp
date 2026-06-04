using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NativeLambdaRouter;
using NativeMediator;

namespace Native.Mcp.NativeLambdaRouter;

/// <summary>
/// A <see cref="RoutedApiGatewayFunction"/> that hosts a Native.Mcp server on a single
/// <c>POST</c> route (default <c>/mcp</c>). The router owns routing, claims extraction, health
/// checks and the Lambda entry point; this function forwards the request body to the MCP
/// <see cref="McpJsonRpcDispatcher"/> and returns its response verbatim.
/// </summary>
/// <remarks>
/// Authentication is the API Gateway JWT Authorizer's job (edge); per-tool authorization lives
/// in the tools (<see cref="McpExecutionContext.HasScope"/>). The MCP route is therefore
/// registered as <c>AllowAnonymous</c> at the router so there is no redundant auth layer — the
/// validated claims still flow through to the tools.
/// Override <see cref="McpPath"/> to change the route.
/// </remarks>
public class McpRoutedApiGatewayFunction : RoutedApiGatewayFunction
{
    private static readonly Dictionary<string, string> JsonHeaders =
        new(StringComparer.OrdinalIgnoreCase) { ["content-type"] = "application/json" };

    private readonly IServiceProvider _serviceProvider;

    /// <summary>Initializes a new <see cref="McpRoutedApiGatewayFunction"/>.</summary>
    /// <param name="serviceProvider">The root service provider (from <c>AddNativeMcpServer</c>).</param>
    public McpRoutedApiGatewayFunction(IServiceProvider serviceProvider)
        : base(NoOpMediator.Instance)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>The route path the MCP endpoint is mapped to. Defaults to <c>/mcp</c>.</summary>
    protected virtual string McpPath => "/mcp";

    /// <inheritdoc/>
    protected override void ConfigureRoutes(IRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.Map<McpRouteRequest, McpRouteResult>(
                global::NativeLambdaRouter.HttpMethod.POST, McpPath, static ctx => new McpRouteRequest(ctx))
            .AllowAnonymous();
    }

    /// <inheritdoc/>
    protected override async Task<object> ExecuteCommandAsync(
        RouteMatch match, RouteContext context, IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<McpJsonRpcDispatcher>();

        var requestContext = McpRouteContextMapper.ToRequestContext(context);
        var result = await dispatcher.DispatchAsync(context.Body, requestContext).ConfigureAwait(false);

        return new ApiGatewayResponse
        {
            StatusCode = result.HttpStatusCode,
            Body = result.Body,
            Headers = string.IsNullOrEmpty(result.Body) ? null : JsonHeaders,
        };
    }

    /// <inheritdoc/>
    protected override string SerializeResponse(object response)
    {
        ArgumentNullException.ThrowIfNull(response);

        // MCP responses are returned as ApiGatewayResponse (raw) and bypass this method; only the
        // router's own framework responses (health, 404, errors) reach here.
        return response switch
        {
            ErrorResponse error => JsonSerializer.Serialize(error, RouterJsonContext.Default.ErrorResponse),
            HealthCheckResponse health => JsonSerializer.Serialize(health, RouterJsonContext.Default.HealthCheckResponse),
            RouteNotFoundResponse notFound => JsonSerializer.Serialize(notFound, RouterJsonContext.Default.RouteNotFoundResponse),
            _ => throw new NotSupportedException($"No serializer registered for '{response.GetType().Name}'."),
        };
    }
}

/// <summary>Marker command for the MCP route (dispatch is handled directly, not via mediator).</summary>
/// <param name="Context">The router request context.</param>
internal sealed record McpRouteRequest(RouteContext Context);

/// <summary>Marker response type for the MCP route registration.</summary>
internal sealed record McpRouteResult;
