// -----------------------------------------------------------------------
// <copyright file="ClusterHeartBeat.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Gossip
{
    public record SetGossipStateKey(string Key, IMessage Value);

    public record SendGossipState;

    public class Gossiper
    {
        public const string GossipActorName = "Gossip";
        private readonly Cluster _cluster;
        private readonly RootContext _context;

        private ILogger _logger = Log.CreateLogger<Gossiper>();
        private PID _pid = null!;

        public Gossiper(Cluster cluster)
        {
            _cluster = cluster;
            _context = _cluster.System.Root;
        }

        public void SetState(string key, IMessage value)
        {
            if (_pid == null)
            {
                return;
            }
            
            _context.Send(_pid, new SetGossipStateKey(key, value));
        }

        internal Task StartAsync()
        {
            var props = Props.FromProducer(() => new GossipActor());
            _pid = _context.SpawnNamed(props, GossipActorName);
            _logger.LogInformation("Started Cluster Gossip");
            _ = SafeTask.Run(GossipLoop);
            return Task.CompletedTask;
        }

        private async Task GossipLoop()
        {
            await Task.Yield();
        
            while (!_cluster.System.Shutdown.IsCancellationRequested)
            {
                try
                {
                    
                    await Task.Delay(_cluster.Config.HeartBeatInterval);
                    SetState("heartbeat", new MemberHeartbeat());
                    SendState();
                    
                }
                catch (Exception x)
                {
                    _logger.LogError(x, "Gossip loop failed");
                }
            }
        }

        private void SendState()
        {
            //just make sure a cluster client cant send
            if (_pid == null)
            {
                return;
            }
            
            _context.Send(_pid, new SendGossipState());
        }

        internal Task ShutdownAsync()
        {
            _logger.LogInformation("Shutting down heartbeat");
            _context.Stop(_pid);
            _logger.LogInformation("Shut down heartbeat");
            return Task.CompletedTask;
        }
    }
}