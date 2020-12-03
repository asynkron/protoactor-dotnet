// -----------------------------------------------------------------------
// <copyright file="GetPid.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using Proto.Router;

namespace Proto.Cluster.Identity
{
    internal class GetPid : IHashable
    {
        public GetPid(ClusterIdentity clusterIdentity, CancellationToken cancellationToken)
        {
            ClusterIdentity = clusterIdentity;
            CancellationToken = cancellationToken;
        }

        public ClusterIdentity ClusterIdentity { get; }
        public CancellationToken CancellationToken { get; }

        public string HashBy() => ClusterIdentity.ToShortString();
    }

    internal class PidResult
    {
        public PID? Pid { get; set; }
    }
}