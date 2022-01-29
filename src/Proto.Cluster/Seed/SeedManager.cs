// -----------------------------------------------------------------------
// <copyright file = "SeedManager.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;

namespace Proto.Cluster.Seed
{
    public static class SeedManager
    {
        public static async Task JoinSeedNode(this Cluster cluster, string host, int port)
        {
            var (h, p) = cluster.System.GetAddress();
            var selfMember = new Member()
            {
                Id = cluster.System.Id,
                Host = h,
                Port = p,
                Kinds = { cluster.GetClusterKinds() }
            };
            
            var pid = PID.FromAddress(host + ":" + port, "seed");
            var res = await cluster.System.Root.RequestAsync<JoinResponse>(pid, new JoinRequest()
                {
                    Joiner = selfMember
                }
            );
            Console.WriteLine("Joined seed node!");
        }
    }
}