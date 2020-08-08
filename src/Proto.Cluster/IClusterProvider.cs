// -----------------------------------------------------------------------
//   <copyright file="IClusterProvider.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Cluster
{
    [PublicAPI]
    public interface IClusterProvider
    {
        Task StartAsync(Cluster cluster, string clusterName, string host, int port, string[] kinds,
            IMemberStatusValue? statusValue, IMemberStatusValueSerializer serializer, MemberList memberList);

        Task ShutdownAsync(Cluster cluster);
    }
}
