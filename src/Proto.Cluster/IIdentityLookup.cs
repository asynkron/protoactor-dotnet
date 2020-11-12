// -----------------------------------------------------------------------
//   <copyright file="IClusterProvider.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.IdentityLookup
{
    public interface IIdentityLookup
    {
        Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct);

        Task RemovePidAsync(PID pid, CancellationToken ct);
        
        Task SetupAsync(Cluster cluster, string[] kinds, bool isClient);
        Task ShutdownAsync();
    }
}