// -----------------------------------------------------------------------
// <copyright file="DiagnosticTools.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Diagnostics
{
    [PublicAPI]
    public static  class DiagnosticTools
    {
        public static async Task<string> GetDiagnosticsString(ActorSystem system, PID pid)
        {
            var request = new ProcessDiagnosticsRequest();
            var res = await system.Root.RequestAsync<ProcessDiagnosticsResponse>(pid, request);
            return res.DiagnosticsString;
        }
    }
}