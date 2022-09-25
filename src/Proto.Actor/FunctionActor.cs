// -----------------------------------------------------------------------
// <copyright file="FunctionActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto;

/// <summary>
///     Used when creating actors from a Func
/// </summary>
internal class FunctionActor : IActor
{
    private readonly Receive _receive;

    public FunctionActor(Receive receive)
    {
        _receive = receive;
    }

    public Task ReceiveAsync(IContext context) => _receive(context);
}