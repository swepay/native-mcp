using Shouldly;

namespace Native.Mcp.Tests.Contracts;

public sealed class McpExecutionContextTests
{
    private static McpExecutionContext Build(IReadOnlyDictionary<string, string> claims) => new(
        toolName: "t",
        requestId: "r",
        correlationId: "c",
        claims: claims,
        rawJwt: "jwt",
        startedAt: DateTimeOffset.UtcNow);

    [Fact]
    public void Scopes_ParsedFromSpaceDelimitedScopeClaim()
    {
        var ctx = Build(new Dictionary<string, string> { ["scope"] = "a b c" });

        ctx.Scopes.ShouldBe(new[] { "a", "b", "c" });
        ctx.HasScope("b").ShouldBeTrue();
        ctx.HasScope("d").ShouldBeFalse();
    }

    [Fact]
    public void Scopes_FallBackToScpClaim()
    {
        var ctx = Build(new Dictionary<string, string> { ["scp"] = "read write" });

        ctx.Scopes.ShouldBe(new[] { "read", "write" });
    }

    [Fact]
    public void SubjectAndAudience_ReadFromClaims()
    {
        var ctx = Build(new Dictionary<string, string> { ["sub"] = "user-1", ["aud"] = "api" });

        ctx.Subject.ShouldBe("user-1");
        ctx.Audience.ShouldBe("api");
    }

    [Fact]
    public void NoClaims_YieldsEmptyScopesAndSubject()
    {
        var ctx = Build(new Dictionary<string, string>());

        ctx.Scopes.ShouldBeEmpty();
        ctx.Subject.ShouldBeEmpty();
        ctx.Audience.ShouldBeEmpty();
        ctx.HasScope("x").ShouldBeFalse();
    }
}
