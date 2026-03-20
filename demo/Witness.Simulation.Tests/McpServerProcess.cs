using System.Diagnostics;
using System.Text.Json;

namespace Witness.Simulation.Tests;

/// <summary>
/// Wraps the .NET MCP server process and provides JSON-RPC communication over stdio.
/// </summary>
public sealed class McpServerProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private int _nextId = 0;

    private McpServerProcess(Process process)
    {
        _process = process;
        _stdin = process.StandardInput;
        _stdout = process.StandardOutput;
    }

    public static async Task<McpServerProcess> StartAsync()
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "src", "Witness.McpServer", "Witness.McpServer.csproj");

        var psi = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\" --no-build")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repoRoot
        };

        var process = new Process { StartInfo = psi };
        process.Start();

        var server = new McpServerProcess(process);

        // Initialize the MCP session
        await server.SendRequestAsync(new
        {
            jsonrpc = "2.0",
            id = server.NextId(),
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "simulation-test", version = "1.0.0" }
            }
        });

        return server;
    }

    public int NextId() => Interlocked.Increment(ref _nextId);

    public async Task<JsonDocument> SendRequestAsync(object request)
    {
        var json = JsonSerializer.Serialize(request);
        await _stdin.WriteLineAsync(json);
        await _stdin.FlushAsync();

        // Read lines until we get a complete JSON response matching the request id
        var requestId = ExtractId(request);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!cts.IsCancellationRequested)
        {
            var line = await _stdout.ReadLineAsync(cts.Token);
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var doc = JsonDocument.Parse(line);
                if (requestId == null) return doc;

                if (doc.RootElement.TryGetProperty("id", out var idProp) &&
                    idProp.ToString() == requestId.ToString())
                {
                    return doc;
                }
            }
            catch (JsonException)
            {
                // incomplete line, keep reading
            }
        }

        throw new TimeoutException($"No response received within timeout for request id={requestId}");
    }

    /// <summary>Parses the tool result text from a tools/call response.</summary>
    public static JsonElement ParseToolResult(JsonDocument response)
    {
        if (response.RootElement.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null)
            throw new InvalidOperationException($"RPC error: {err.GetRawText()}");

        var text = response.RootElement
            .GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()!;

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static object? ExtractId(object request)
    {
        var json = JsonSerializer.Serialize(request);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.Clone() : null;
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
        throw new DirectoryNotFoundException("Could not find repo root (.git or LICENSE not found)");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _stdin.Close();
            if (!_process.WaitForExit(3000))
                _process.Kill();
        }
        catch { /* best effort */ }

        await Task.CompletedTask;
    }
}
