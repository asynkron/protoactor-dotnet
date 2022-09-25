// -----------------------------------------------------------------------
// <copyright file="Behavior.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto;

/// <summary>
///     Utility class for creating state machines. See <a href="https://proto.actor/docs/behaviors/">Behaviors</a> for more
///     information.
/// </summary>
public class Behavior
{
    private readonly Stack<Receive> _behaviors = new();

    /// <summary>
    ///     Initializes the behavior with an initial message handler. Use <see cref="Become" /> to set initial message handler.
    /// </summary>
    public Behavior()
    {
    }

    /// <summary>
    ///     Initializes the behavior with an initial message handler.
    /// </summary>
    /// <param name="receive">Function to process actor's messages</param>
    public Behavior(Receive receive)
    {
        Become(receive);
    }

    /// <summary>
    ///     Switches to a new behavior. Previous behavior stack is cleared.
    /// </summary>
    /// <param name="receive">Function to process actor's messages</param>
    public void Become(Receive receive)
    {
        _behaviors.Clear();
        _behaviors.Push(receive);
    }

    /// <summary>
    ///     Switches to a new behavior. Previous behavior is stored on a stack.
    /// </summary>
    /// <param name="receive"></param>
    public void BecomeStacked(Receive receive) => _behaviors.Push(receive);

    /// <summary>
    ///     Restores previous behavior from the stack.
    /// </summary>
    public void UnbecomeStacked() => _behaviors.Pop();

    /// <summary>
    ///     Handle the message with currently active message handler (behavior).
    /// </summary>
    /// <param name="context">Actor context to process</param>
    /// <returns></returns>
    public Task ReceiveAsync(IContext context)
    {
        var behavior = _behaviors.Peek();

        return behavior(context);
    }
}