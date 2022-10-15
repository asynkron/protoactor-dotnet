using System.Collections.Concurrent;

namespace Proto.Diagnostics;

public class DiagnosticsStore
{
    private readonly ConcurrentBag<DiagnosticsEntry> _entries = new();

    public void RegisterEvent(string module, string message)
    {
        var entry = new DiagnosticsEntry(module, message);
        _entries.Add(entry);
    }

    public DiagnosticsEntry[] Get()
    {
        return _entries.ToArray();
    }
}

public record DiagnosticsEntry(string Module, string Message);