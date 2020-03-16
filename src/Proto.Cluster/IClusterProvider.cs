// -----------------------------------------------------------------------
//   <copyright file="IClusterProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto.Cluster
{
    public interface IClusterProvider
    {
        Task RegisterMemberAsync(Cluster cluster, string clusterName, string host, int port, string[] kinds, IMemberStatusValue statusValue, IMemberStatusValueSerializer serializer);
        void MonitorMemberStatusChanges(Cluster cluster);
        Task UpdateMemberStatusValueAsync(Cluster cluster, IMemberStatusValue statusValue);
        Task DeregisterMemberAsync(Cluster cluster);
        Task Shutdown(Cluster cluster);
    }
}