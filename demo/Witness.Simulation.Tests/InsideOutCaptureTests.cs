using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Witness.AspNetCore;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace Witness.Simulation.Tests;

/// <summary>
/// Tests inside-out HTTP capture: interactions initiated by a service (not by the agent).
///
/// Scenario A — Witness.AspNetCore DelegatingHandler:
///   A .NET HttpClient is configured with WitnessCaptureHandler. Any call it makes is
///   intercepted and stored automatically, with no changes required to the calling code.
///
/// Scenario B — Witness.Proxy recording reverse proxy:
///   An HttpClient is pointed at Witness.Proxy (instead of the real API). The proxy
///   forwards every request to the configured upstream and records the interaction.
///   Zero changes required in the service under observation.
///
/// Prerequisites: docker compose up -d
/// </summary>
public sealed class InsideOutCaptureTests
{
    private const string LegacyBase = "http://localhost:3001";
    private const string ModernBase = "http://localhost:3002";

    // ── Scenario A: DelegatingHandler ─────────────────────────────────────────

    [Fact]
    public async Task AspNetCoreHandler_CapturesOutboundGetAndPost()
    {
        var sessionId = $"aspnetcore-test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var storePath = Path.Combine(FindRepoRoot(), "witness-store");

        var options = new WitnessCaptureOptions
        {
            SessionId = sessionId,
            Tag = "outbound",
            StorePath = storePath
        };

        // Wrap a plain HttpClientHandler with the capture handler — no DI needed
        var handler = new WitnessCaptureHandler(options, new HttpClientHandler());
        using var client = new HttpClient(handler) { BaseAddress = new Uri(LegacyBase) };

        // Act — GET product
        var getResponse = await client.GetAsync("/api/products/1");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadAsStringAsync();
        var getJson = JsonDocument.Parse(getBody).RootElement;
        getJson.GetProperty("unit_price").GetDouble().Should().Be(9.99);
        getJson.GetProperty("stock").GetInt32().Should().Be(100);

        // Act — POST order
        var postContent = new StringContent(
            """{"product_id":1,"qty":2}""",
            System.Text.Encoding.UTF8, "application/json");
        var postResponse = await client.PostAsync("/api/orders", postContent);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var postBody = await postResponse.Content.ReadAsStringAsync();
        var postJson = JsonDocument.Parse(postBody).RootElement;
        postJson.GetProperty("order_id").GetInt32().Should().Be(1001);
        postJson.GetProperty("status").GetString().Should().Be("pending");

        // Assert — both interactions were recorded
        var repo = BuildRepository(storePath);
        var interactions = await repo.ListBySessionAsync(sessionId);
        interactions.Should().HaveCount(2);
        interactions.Should().Contain(i => i.Request.Method == "GET" && i.Response.StatusCode == 200);
        interactions.Should().Contain(i => i.Request.Method == "POST" && i.Response.StatusCode == 201);
    }

    [Fact]
    public async Task AspNetCoreHandler_CapturesCallsToModernApi()
    {
        var sessionId = $"aspnetcore-modern-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var storePath = Path.Combine(FindRepoRoot(), "witness-store");

        var options = new WitnessCaptureOptions
        {
            SessionId = sessionId,
            Tag = "outbound-modern",
            StorePath = storePath
        };

        var handler = new WitnessCaptureHandler(options, new HttpClientHandler());
        using var client = new HttpClient(handler) { BaseAddress = new Uri(ModernBase) };

        var response = await client.GetAsync("/api/products/1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("price").GetDouble().Should().Be(9.99);
        body.GetProperty("currency").GetString().Should().Be("USD");

        var repo = BuildRepository(storePath);
        var interactions = await repo.ListBySessionAsync(sessionId);
        interactions.Should().HaveCount(1);
        interactions[0].Metadata.Tags.Should().Contain("outbound-modern");
    }

    // ── Scenario B: Witness.Proxy ──────────────────────────────────────────────

    [Fact]
    public async Task Proxy_CapturesForwardedGetAndPost_LegacyUpstream()
    {
        var sessionId = $"proxy-legacy-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var storePath = Path.Combine(FindRepoRoot(), "witness-store");
        var port = 19001; // high port to avoid conflicts

        await using var proxy = await ProxyProcess.StartAsync(
            upstream: LegacyBase,
            port: port,
            sessionId: sessionId,
            storePath: storePath);

        using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

        // Act — GET through proxy
        var getResponse = await client.GetAsync("/api/products/1");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync()).RootElement;
        getBody.GetProperty("unit_price").GetDouble().Should().Be(9.99);

        // Act — POST through proxy
        var postContent = new StringContent(
            """{"product_id":1,"qty":2}""",
            System.Text.Encoding.UTF8, "application/json");
        var postResponse = await client.PostAsync("/api/orders", postContent);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert — recorded
        var repo = BuildRepository(storePath);
        var interactions = await repo.ListBySessionAsync(sessionId);
        interactions.Should().HaveCount(2);
        interactions.Should().Contain(i => i.Request.Method == "GET" && i.Response.StatusCode == 200);
        interactions.Should().Contain(i => i.Request.Method == "POST" && i.Response.StatusCode == 201);
    }

    [Fact]
    public async Task Proxy_CapturesForwardedGet_ModernUpstream()
    {
        var sessionId = $"proxy-modern-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var storePath = Path.Combine(FindRepoRoot(), "witness-store");
        var port = 19002;

        await using var proxy = await ProxyProcess.StartAsync(
            upstream: ModernBase,
            port: port,
            sessionId: sessionId,
            storePath: storePath);

        using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

        var response = await client.GetAsync("/api/products/1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("price").GetDouble().Should().Be(9.99);
        body.GetProperty("currency").GetString().Should().Be("USD");

        var repo = BuildRepository(storePath);
        var interactions = await repo.ListBySessionAsync(sessionId);
        interactions.Should().HaveCount(1);
        interactions[0].Response.StatusCode.Should().Be(200);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FileSystemInteractionRepository BuildRepository(string storePath)
    {
        var opts = Options.Create(new WitnessOptions
        {
            Storage = new StorageOptions { Path = storePath }
        });
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemInteractionRepository>.Instance;
        return new FileSystemInteractionRepository(opts, logger);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "LICENSE")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repo root");
    }
}

/// <summary>
/// Spawns Witness.Proxy as a child process for the duration of a test.
/// </summary>
internal sealed class ProxyProcess : IAsyncDisposable
{
    private readonly Process _process;

    private ProxyProcess(Process process) => _process = process;

    public static async Task<ProxyProcess> StartAsync(
        string upstream, int port, string sessionId, string storePath)
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "src", "Witness.Proxy", "Witness.Proxy.csproj");

        var args = string.Join(" ",
            $"run --project \"{projectPath}\" --no-build",
            $"-- --Proxy:Port={port}",
            $"--Proxy:Upstream={upstream}",
            $"--Proxy:SessionId={sessionId}",
            $"--Proxy:StorePath={storePath}");

        var psi = new ProcessStartInfo("dotnet", args)
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        var process = new Process { StartInfo = psi };
        process.Start();

        // Wait until the proxy port is reachable (TCP only — HTTP probes would be recorded)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!cts.IsCancellationRequested)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                await tcp.ConnectAsync("localhost", port, cts.Token);
                break;
            }
            catch { await Task.Delay(200, cts.Token); }
        }

        return new ProxyProcess(process);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        catch { /* best effort */ }
        _process.Dispose();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "LICENSE")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find repo root");
    }
}
