using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace Native.Mcp.Tests.Envelope;

public sealed class EnvelopeAndProblemTests
{
    private static SwepayEnvelopeMetadata Metadata() => new(
        RequestId: "req-1",
        CorrelationId: "corr-1",
        Timestamp: "2026-06-03T14:30:00.000Z",
        DurationMs: 42,
        ServerName: "srv",
        ServerVersion: "1.0.0");

    [Fact]
    public void BuildSuccess_ProducesCanonicalShape()
    {
        var data = new JsonObject { ["status"] = "ok" };

        var json = SwepayEnvelopeFactory.BuildSuccess(data, Metadata());

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("data").GetProperty("status").GetString().Should().Be("ok");
        root.GetProperty("error").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("metadata").GetProperty("requestId").GetString().Should().Be("req-1");
        root.GetProperty("metadata").GetProperty("durationMs").GetInt64().Should().Be(42);
    }

    [Fact]
    public void BuildFailure_ProducesProblemDetails()
    {
        var problem = McpProblems.ValidationFailed("bad input", "req-1");

        var json = SwepayEnvelopeFactory.BuildFailure(problem, Metadata());

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
        var error = root.GetProperty("error");
        error.GetProperty("type").GetString().Should().Be(ProblemTypes.ValidationFailed);
        error.GetProperty("status").GetInt32().Should().Be(400);
        error.GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
    }

    [Theory]
    [InlineData("https://errors.swepay.com.br/common/validation-failed", "VALIDATION_FAILED")]
    [InlineData("https://errors.swepay.com.br/common/not-found", "NOT_FOUND")]
    [InlineData("internal-error", "INTERNAL_ERROR")]
    public void DeriveCode_DerivesUpperSnakeFromLastSegment(string type, string expected)
    {
        SwepayProblemDetails.DeriveCode(type).Should().Be(expected);
    }

    [Fact]
    public void Create_FillsDefaultsForOptionalFields()
    {
        var problem = SwepayProblemDetails.Create(
            ProblemTypes.Conflict, "Conflict", 409, "Already exists");

        problem.Code.Should().Be("CONFLICT");
        problem.Recovery.Should().NotBeNullOrWhiteSpace();
        problem.RequestId.Should().BeEmpty();
        problem.Instance.Should().BeNull();
    }
}
