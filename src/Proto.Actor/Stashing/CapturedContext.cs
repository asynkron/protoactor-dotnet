// -----------------------------------------------------------------------
// <copyright file="StashedMessage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto;

/// <summary>
///     Holds a reference to actor context and a message
/// </summary>
/// <param name="MessageEnvelope">Message to store</param>
/// <param name="Context">Context to store</param>
public record CapturedContext(MessageEnvelope MessageEnvelope, IContext Context)
{
    /// <summary>
    ///     Reprocesses the captured message on the captured context.
    ///     It captures current context before processing and restores it after processing.
    /// </summary>
    public async Task Receive()
    {
        var current = Context.Capture();
        await Context.Receive(MessageEnvelope).ConfigureAwait(false);
        current.Apply();
    }

    /// <summary>
    ///     Restores the stored message to the actor context so that it can be re-processed by the actor
    /// </summary>
    public void Apply() => Context.Apply(this);
}