using FluentAssertions;

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

        ctx.Scopes.Should().Equal("a", "b", "c");
        ctx.HasScope("b").Should().BeTrue();
        ctx.HasScope("d").Should().BeFalse();
    }

    [Fact]
    public void Scopes_FallBackToScpClaim()
    {
        var ctx = Build(new Dictionary<string, string> { ["scp"] = "read write" });

        ctx.Scopes.Should().Equal("read", "write");
    }

    [Fact]
    public void SubjectAndAudience_ReadFromClaims()
    {
        var ctx = Build(new Dictionary<string, string> { ["sub"] = "user-1", ["aud"] = "api" });

        ctx.Subject.Should().Be("user-1");
        ctx.Audience.Should().Be("api");
    }

    [Fact]
    public void NoClaims_YieldsEmptyScopesAndSubject()
    {
        var ctx = Build(new Dictionary<string, string>());

        ctx.Scopes.Should().BeEmpty();
        ctx.Subject.Should().BeEmpty();
        ctx.Audience.Should().BeEmpty();
        ctx.HasScope("x").Should().BeFalse();
    }
}
