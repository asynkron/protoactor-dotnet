// -----------------------------------------------------------------------
// <copyright file="IMessageAutoAck.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto;

/// <summary>
///     A message implementing with IAutoRespond will not be passed to actor's Receive method, but instead an automatic
///     response will be returned to the sender.
/// </summary>
public interface IAutoRespond
{
    /// <summary>
    ///     Gets the automatic response to return to the sender
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    object GetAutoResponse(IContext context);
}