// -----------------------------------------------------------------------
// <copyright file="IRemote.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using Proto.Extensions;

namespace Proto.Remote
{
    public interface IRemote : IActorSystemExtension<IRemote>
    {
        RemoteConfigBase Config { get; }
        ActorSystem System { get; }
        bool Started { get; }
        Task ShutdownAsync(bool graceful = true);
        Task StartAsync();
    }
}