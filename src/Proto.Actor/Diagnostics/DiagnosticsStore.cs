using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;


namespace Proto.Diagnostics;

public class DiagnosticsStore
{
    private readonly ActorSystem _system;
    private readonly ILogger _logger = Log.CreateLogger<DiagnosticsStore>();
    private readonly ConcurrentSet<DiagnosticsEntry> _entries = new();
    private readonly LogLevel _logLevel;

    public DiagnosticsStore(ActorSystem system)
    {
        _system = system;
        _logLevel = system.Config.DiagnosticsLogLevel;
        RegisterEnvironmentSettings();
    }

    private void RegisterEnvironmentSettings()
    {
        ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);
        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableCompletionPortThreads);
        var cpuCount = Environment.ProcessorCount;
        var dotnetVersion = Environment.Version;
        var platform = Environment.OSVersion.Platform;
        var platformVersion = Environment.OSVersion.VersionString;
        var stats = new
        {
            cpuCount,
            dotnetVersion,
            platform,
            platformVersion,
            minWorkerThreads,
            minCompletionPortThreads,
            availableWorkerThreads,
            availableCompletionPortThreads,
        };

        RegisterObject("Environment", "Settings", stats);
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

    public async Task<DiagnosticsEntry[]> GetDiagnostics()
    {
        var entries = new List<DiagnosticsEntry>();
        var extensions = _system.Extensions.GetAll().ToArray();

        foreach (var e in extensions)
        {
            var res = await e.GetDiagnostics().ConfigureAwait(false);
            entries.AddRange(res);
        }
        entries.AddRange(_entries.ToArray());

        return entries.ToArray();
    }
}