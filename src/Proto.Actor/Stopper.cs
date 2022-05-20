// -----------------------------------------------------------------------
// <copyright file = "Stopper.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;

namespace Proto;

public class Stopper
{
    private readonly CancellationTokenSource _cts = new();
    private string _reason = "";

    public void Stop(string reason = "")
    {
        _cts.Cancel();
        _reason = reason;
    }

    public CancellationToken Token => _cts.Token;
}