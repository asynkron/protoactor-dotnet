// -----------------------------------------------------------------------
// <copyright file="IReceiverContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Proto;

public interface IReceiverContext : IInfoContext
{
    /// <summary>
    ///     Receive abstraction used in middlewares
    /// </summary>
    /// <param name="envelope">The received envelope</param>
    /// <returns></returns>
    Task Receive(MessageEnvelope envelope);
}