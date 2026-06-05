using Shouldly;

namespace Native.Mcp.Tests.Errors;

public sealed class ErrorMapperTests
{
    [Fact]
    public void MapException_ReturnsInternalError_WithoutLeakingMessage()
    {
        var problem = McpErrorMapper.MapException(new InvalidOperationException("secret-detail"), "req-7");

        problem.Type.ShouldBe(ProblemTypes.InternalError);
        problem.Status.ShouldBe(500);
        problem.RequestId.ShouldBe("req-7");
        problem.Detail.ShouldNotContain("secret-detail");
    }

    [Fact]
    public void Forbidden_ProducesForbiddenProblem()
    {
        var problem = McpProblems.Forbidden("Required scope: x", "req-1");

        problem.Type.ShouldBe(ProblemTypes.Forbidden);
        problem.Status.ShouldBe(403);
        problem.Code.ShouldBe("FORBIDDEN");
    }
}
