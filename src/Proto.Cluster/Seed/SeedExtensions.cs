// -----------------------------------------------------------------------
// <copyright file = "SeedManager.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Seed
{
    public static class SeedExtensions
    {
        public static Task JoinSeed(this Cluster cluster,string host, int port) => JoinSeed(cluster, (host, port));

        public static async Task JoinSeed(this Cluster cluster, params  (string host, int port)[] seeds)
        {
            if (seeds.Length == 0)
            {
                throw new ArgumentException("Seed nodes may not be empty", nameof(seeds));
            }
            
            var logger = Log.CreateLogger("SeedNode");
            var (selfHost, selfPort) = cluster.System.GetAddress();
            foreach (var (host, port) in seeds)
            {
                //never connect to yourself
                if (host == selfHost && port == selfPort)
                {
                    continue;
                }
                
                try
                {
                    var pid = PID.FromAddress(host + ":" + port, "seed");
                    var res = await cluster.System.Root.RequestAsync<JoinResponse>(pid, new JoinRequest
                        {
                            Joiner = cluster.MemberList.Self
                        }
                    );
                    logger.LogInformation("Connected to seed node {Host}:{Port}", host, port);
                    return;
                }
                catch(Exception x)
                {
                    logger.LogError(x, "Failed to connect to seed node {Host}:{Port}", host, port);
                }
            }

            throw new Exception("Failed to join any seed node");
        }
    }
}