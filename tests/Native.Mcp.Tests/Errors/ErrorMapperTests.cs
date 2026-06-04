using FluentAssertions;

namespace Native.Mcp.Tests.Errors;

public sealed class ErrorMapperTests
{
    [Fact]
    public void MapException_ReturnsInternalError_WithoutLeakingMessage()
    {
        var problem = McpErrorMapper.MapException(new InvalidOperationException("secret-detail"), "req-7");

        problem.Type.Should().Be(ProblemTypes.InternalError);
        problem.Status.Should().Be(500);
        problem.RequestId.Should().Be("req-7");
        problem.Detail.Should().NotContain("secret-detail");
    }

    [Fact]
    public void Forbidden_ProducesForbiddenProblem()
    {
        var problem = McpProblems.Forbidden("Required scope: x", "req-1");

        problem.Type.Should().Be(ProblemTypes.Forbidden);
        problem.Status.Should().Be(403);
        problem.Code.Should().Be("FORBIDDEN");
    }
}
