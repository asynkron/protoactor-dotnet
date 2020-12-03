// -----------------------------------------------------------------------
// <copyright file="IReceiverContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public interface IReceiverContext : IInfoContext
    {
        Task Receive(MessageEnvelope envelope);
    }
}