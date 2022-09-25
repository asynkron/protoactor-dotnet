// -----------------------------------------------------------------------
// <copyright file = "Stopper.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;

namespace Proto;

internal class Stopper
{
    private readonly CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    public string StoppedReason { get; private set; } = "";

    public void Stop(string reason = "")
    {
        _cts.Cancel();
        StoppedReason = reason;
    }
}