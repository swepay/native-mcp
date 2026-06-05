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
    public void Roles_ParsedFromCommaSeparatedRolesClaim()
    {
        var ctx = Build(new Dictionary<string, string> { ["roles"] = "admin,trial-provisioner" });

        ctx.Roles.ShouldBe(new[] { "admin", "trial-provisioner" });
        ctx.HasRole("admin").ShouldBeTrue();
        ctx.HasRole("missing").ShouldBeFalse();
        ctx.HasAnyRole("x", "trial-provisioner").ShouldBeTrue();
    }

    [Fact]
    public void Roles_ParsedFromSingleRoleClaim()
    {
        var ctx = Build(new Dictionary<string, string> { ["role"] = "operator" });

        ctx.Roles.ShouldBe(new[] { "operator" });
        ctx.HasRole("operator").ShouldBeTrue();
    }

    [Fact]
    public void Roles_ParsedFromJsonArrayClaim()
    {
        var ctx = Build(new Dictionary<string, string> { ["cognito:groups"] = """["a","b"]""" });

        ctx.Roles.ShouldBe(new[] { "a", "b" });
    }

    [Fact]
    public void Roles_MergedAndDeduplicatedAcrossClaimTypes()
    {
        var ctx = Build(new Dictionary<string, string>
        {
            ["role"] = "admin",
            ["roles"] = "admin,reader",
            ["groups"] = "writer",
        });

        ctx.Roles.ShouldBe(new[] { "admin", "reader", "writer" });
    }

    [Fact]
    public void RequireRole_ThrowsForbiddenWhenMissing()
    {
        var ctx = Build(new Dictionary<string, string> { ["roles"] = "reader" });

        var ex = Should.Throw<McpForbiddenException>(() => ctx.RequireRole("writer"));
        ex.RequiredRole.ShouldBe("writer");
    }

    [Fact]
    public void RequireRole_DoesNotThrowWhenPresent()
    {
        var ctx = Build(new Dictionary<string, string> { ["roles"] = "writer" });

        Should.NotThrow(() => ctx.RequireRole("writer"));
    }

    [Fact]
    public void HasClaim_MatchesExactAndDelimitedValues()
    {
        var ctx = Build(new Dictionary<string, string> { ["aud"] = "api gateway", ["tier"] = "gold" });

        ctx.HasClaim("tier", "gold").ShouldBeTrue();
        ctx.HasClaim("aud", "gateway").ShouldBeTrue();
        ctx.HasClaim("aud", "missing").ShouldBeFalse();
    }

    [Fact]
    public void SubjectAndAudience_ReadFromClaims()
    {
        var ctx = Build(new Dictionary<string, string> { ["sub"] = "user-1", ["aud"] = "api" });

        ctx.Subject.ShouldBe("user-1");
        ctx.Audience.ShouldBe("api");
    }

    [Fact]
    public void NoClaims_YieldsEmptyRolesAndSubject()
    {
        var ctx = Build(new Dictionary<string, string>());

        ctx.Roles.ShouldBeEmpty();
        ctx.Subject.ShouldBeEmpty();
        ctx.Audience.ShouldBeEmpty();
        ctx.HasRole("x").ShouldBeFalse();
    }
}
