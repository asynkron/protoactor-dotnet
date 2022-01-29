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
            
            
            var pid = PID.FromAddress(host + ":" + port, "seed");
            var res = await cluster.System.Root.RequestAsync<JoinResponse>(pid, new JoinRequest()
                {
                    Joiner = cluster.MemberList.Self
                }
            );
            Console.WriteLine("Joined seed node!");
        }
    }
}