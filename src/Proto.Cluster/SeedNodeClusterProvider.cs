// -----------------------------------------------------------------------
// <copyright file = "SeedNodeClusterProvider.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster
{
    public class SeedNodeClusterProvider : IClusterProvider
    {
        private CancellationTokenSource _cts = new();
        
        public Task StartMemberAsync(Cluster cluster)
        {
            _ = SafeTask.Run(async () => {

                }
            );

            return Task.CompletedTask;
        }

        public Task StartClientAsync(Cluster cluster)
        {
            throw new NotImplementedException();
        }

        public Task ShutdownAsync(bool graceful)
        {
           _cts.Cancel();
           return Task.CompletedTask;
        }
    }
}