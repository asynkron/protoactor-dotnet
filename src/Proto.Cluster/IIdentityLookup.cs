// -----------------------------------------------------------------------
//   <copyright file="IClusterProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Cluster.IdentityLookup
{
    public interface IIdentityLookup
    {
        Task<(PID?,ResponseStatusCode)> GetAsync(string identity,string kind, CancellationToken ct);
        void Setup(Cluster cluster, string[] kinds);
        void Stop();
    }
}