// -----------------------------------------------------------------------
// <copyright file="Remote.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;

namespace Proto.Remote.GrpcNet
{
    public static class Remote
    {
        public static Func<ActorSystem,Task> Config(GrpcNetRemoteConfig remoteConfig) => async s => {
            s.WithRemote(remoteConfig);
            await s.Remote().StartAsync();
        };
    }
}