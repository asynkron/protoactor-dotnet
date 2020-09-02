// -----------------------------------------------------------------------
//   <copyright file="Extensions.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;

namespace Proto.Remote
{
    public static class Extensions
    {
        public static IRemote AddRemote(this ActorSystem actorSystem, string hostname, int port,
            Action<RemoteConfiguration>? configure = null)
        {
            var remote = new SelfHostedRemote(actorSystem, hostname, port, configure);
            return remote;
        }
    }
}