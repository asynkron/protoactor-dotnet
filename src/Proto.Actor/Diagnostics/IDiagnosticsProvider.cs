using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Diagnostics;

[PublicAPI]
public interface IDiagnosticsProvider
{
    Task<DiagnosticsEntry[]> GetDiagnostics() => Task.FromResult(Array.Empty<DiagnosticsEntry>());
}