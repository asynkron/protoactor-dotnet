// -----------------------------------------------------------------------
// <copyright file="DiagnosticTools.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Diagnostics;

[PublicAPI]
public static class DiagnosticTools
{
    /// <summary>
    ///     Asks an actor (or any other process) to provide diagnostics string by sending a
    ///     <see cref="ProcessDiagnosticsRequest" /> message.
    /// </summary>
    /// <param name="system"></param>
    /// <param name="pid"></param>
    /// <returns></returns>
    public static async Task<string> GetDiagnosticsString(ActorSystem system, PID pid)
    {
        var completionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new ProcessDiagnosticsRequest(completionSource);
        pid.SendSystemMessage(system, request);
        var res = await completionSource.Task.ConfigureAwait(false);

        return res;
    }
}