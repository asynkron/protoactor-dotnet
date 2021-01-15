// -----------------------------------------------------------------------
// <copyright file="IDurablePersistence.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

namespace Proto.Cluster.Durable.FileSystem
{
    public interface IDurablePersistence
    {
        Task StartAsync(Cluster cluster);

        Task PersistRequestAsync(DurableRequest request, object responseMessage);

        Task PersistFunctionStartAsync(ClusterIdentity identity, object requestMessage);
    }
}