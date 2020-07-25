// -----------------------------------------------------------------------
//   <copyright file="IClusterProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Cluster
{
    [PublicAPI]
    public interface IClusterProvider
    {
        Task StartAsync(
            Cluster cluster, string clusterName, string host, int port, string[] kinds,
            IMemberStatusValue? statusValue, IMemberStatusValueSerializer serializer
        );

        Task ShutdownAsync(Cluster cluster);
    }
}
