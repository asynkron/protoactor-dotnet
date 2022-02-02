// -----------------------------------------------------------------------
// <copyright file="IClusterProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Cluster
{
    [PublicAPI]
    public interface IClusterProvider
    {
        Task StartMemberAsync(Cluster cluster);

        Task StartClientAsync(Cluster cluster);

        Task ShutdownAsync(bool graceful);
    }
}