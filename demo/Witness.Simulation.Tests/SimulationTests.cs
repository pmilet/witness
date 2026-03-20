using System.Text.Json;
using FluentAssertions;

namespace Witness.Simulation.Tests;

/// <summary>
/// End-to-end simulation tests validating the Witness MCP workflow:
/// record → replay → list, using two local Docker APIs that simulate
/// a legacy-to-modern API migration scenario.
///
/// Prerequisites: docker compose up -d
///
/// Note: witness/compare is not yet implemented in the .NET server.
/// </summary>
public sealed class SimulationTests
{
    private const string LegacyTarget = "http://localhost:3001";
    private const string ModernTarget = "http://localhost:3002";

    private readonly string _sessionId = $"simulation-test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    [Fact]
    public async Task FullWorkflow_RecordReplayList_ValidatesApiMigration()
    {
        await using var server = await McpServerProcess.StartAsync();

        // ── Scenario 1: Record GET from legacy ──────────────────────────────
        var resp1 = await server.SendRequestAsync(new
        {
            jsonrpc = "2.0",
            id = server.NextId(),
            method = "tools/call",
            @params = new
            {
                name = "witness/record",
                arguments = new
                {
                    target = LegacyTarget,
                    method = "GET",
                    path = "/api/products/1",
                    options = new { Tag = "legacy-get-product", SessionId = _sessionId }
                }
            }
        });
        var r1 = McpServerProcess.ParseToolResult(resp1);
        r1.GetProperty("StatusCode").GetInt32().Should().Be(200, "legacy GET should return 200");

        var body1 = ParseResponseBody(r1);
        body1.Should().ContainKey("unit_price", "legacy schema uses unit_price");
        body1.Should().ContainKey("stock", "legacy schema includes stock");
        var legacyGetWitnessId = r1.GetProperty("WitnessId").GetString()!;
        legacyGetWitnessId.Should().NotBeNullOrEmpty();

        // ── Scenario 2: Record GET from modern ──────────────────────────────
        var resp2 = await server.SendRequestAsync(new
        {
            jsonrpc = "2.0",
            id = server.NextId(),
            method = "tools/call",
            @params = new
            {
                name = "witness/record",
                arguments = new
                {
                    target = ModernTarget,
                    method = "GET",
                    path = "/api/products/1",
                    options = new { Tag = "modern-get-product", SessionId = _sessionId }
                }
            }
        });
        var r2 = McpServerProcess.ParseToolResult(resp2);
        r2.GetProperty("StatusCode").GetInt32().Should().Be(200, "modern GET should return 200");

        var body2 = ParseResponseBody(r2);
        body2.Should().ContainKey("price", "modern schema uses price");
        body2.Should().ContainKey("currency", "modern schema includes currency");

        // ── Scenario 3: Record POST to legacy ───────────────────────────────
        var resp3 = await server.SendRequestAsync(new
        {
            jsonrpc = "2.0",
            id = server.NextId(),
            method = "tools/call",
            @params = new
            {
                name = "witness/record",
                arguments = new
                {
                    target = LegacyTarget,
                    method = "POST",
                    path = "/api/orders",
                    body = new { product_id = 1, qty = 2 },
                    options = new { Tag = "legacy-create-order", SessionId = _sessionId }
                }
            }
        });
        var r3 = McpServerProcess.ParseToolResult(resp3);
        r3.GetProperty("StatusCode").GetInt32().Should().Be(201, "legacy POST should return 201");

        var body3 = ParseResponseBody(r3);
        body3.Should().ContainKey("order_id", "legacy order response uses order_id");
        body3["status"]?.ToString().Should().Be("pending", "legacy order starts as pending");
        var legacyPostWitnessId = r3.GetProperty("WitnessId").GetString()!;
        legacyPostWitnessId.Should().NotBeNullOrEmpty();

        // ── Scenario 4: Replay legacy POST against modern ────────────────────
        var resp4 = await server.SendRequestAsync(new
        {
            jsonrpc = "2.0",
            id = server.NextId(),
            method = "tools/call",
            @params = new
            {
                name = "witness/replay",
                arguments = new
                {
                    witnessId = legacyPostWitnessId,
                    target = ModernTarget,
                    options = new { SessionId = _sessionId }
                }
            }
        });
        var r4 = McpServerProcess.ParseToolResult(resp4);
        r4.GetProperty("StatusCode").GetInt32().Should().Be(201, "modern API replayed with legacy request should return 201");
        r4.GetProperty("ReplayWitnessId").GetString().Should().NotBeNullOrEmpty();

        var replayBody = ParseResponseBody(r4);
        replayBody.Should().ContainKey("orderId", "modern order response uses orderId");
        replayBody.Should().ContainKey("state", "modern order response uses state");

        // ── Scenario 5: List session interactions ────────────────────────────
        var resp5 = await server.SendRequestAsync(new
        {
            jsonrpc = "2.0",
            id = server.NextId(),
            method = "tools/call",
            @params = new
            {
                name = "witness/list",
                arguments = new { sessionId = _sessionId }
            }
        });
        var r5 = McpServerProcess.ParseToolResult(resp5);
        r5.GetProperty("Count").GetInt32().Should().BeGreaterThanOrEqualTo(4,
            "session should contain at least 4 interactions (2 GETs + 1 POST + 1 replay)");
        r5.GetProperty("SessionId").GetString().Should().Be(_sessionId);
    }

    private static Dictionary<string, object?> ParseResponseBody(JsonElement result)
    {
        var bodyProp = result.GetProperty("ResponseBody");

        JsonElement bodyElement = bodyProp.ValueKind == JsonValueKind.String
            ? JsonDocument.Parse(bodyProp.GetString()!).RootElement
            : bodyProp;

        return bodyElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
    }
}
