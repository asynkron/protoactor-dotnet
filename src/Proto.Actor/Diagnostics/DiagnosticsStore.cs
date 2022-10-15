using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.Extensions.Logging;
using Proto.Utils;


namespace Proto.Diagnostics;

public class DiagnosticsStore
{
    private readonly ILogger _logger = Log.CreateLogger<DiagnosticsStore>();
    private readonly ConcurrentSet<DiagnosticsEntry> _entries = new();
    private readonly LogLevel _logLevel;

    public DiagnosticsStore(ActorSystem system)
    {
        _logLevel = system.Config.DiagnosticsLogLevel;
        RegisterThreadPoolStats();
    }

    private void RegisterThreadPoolStats()
    {
        ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);
        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableCompletionPortThreads);

        var stats = new
        {
            minWorkerThreads,
            minCompletionPortThreads,
            availableWorkerThreads,
            availableCompletionPortThreads,
        };

        RegisterObject("ThreadPool", "Threads", stats);
    }

    public void RegisterEvent(string module, string message)
    {
        var entry = new DiagnosticsEntry(module, message, null);
        if (_entries.TryAdd(entry))
        {
            _logger.Log(_logLevel, "[Diagnostics] {Module}: {Message}", module, message);
        }
    }

    public void RegisterObject(string module, string key, object data)
    {
        var entry = new DiagnosticsEntry(module, key, data);

        if (_entries.TryAdd(entry))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            _logger.Log(_logLevel,"[Diagnostics] {Module}: {Key}: {Data}", module, key, json);
        }
    }

    public DiagnosticsEntry[] Get()
    {
        return _entries.ToArray();
    }
}

public record DiagnosticsEntry
{
    // ReSharper disable once ConvertToPrimaryConstructor
    public DiagnosticsEntry(string module, string? message, object? data)
    {
        Module = module;
        Message = message;
        Data = data;
    }

    public string Module { get;  }


    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; }


    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; }
}
