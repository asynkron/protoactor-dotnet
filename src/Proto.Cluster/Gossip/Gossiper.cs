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

    public record SendGossipStateRequest;
    public record SendGossipStateResponse;

    public class Gossiper
    {
        public const string GossipActorName = "gossip";
        private readonly Cluster _cluster;
        private readonly RootContext _context;

        private static readonly ILogger Logger = Log.CreateLogger<Gossiper>();
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
            Logger.LogInformation("Started Cluster Gossip");
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
                    
                    await Task.Delay((int)_cluster.Config.GossipInterval.TotalMilliseconds);
                    SetState("heartbeat", new MemberHeartbeat());
                    await SendStateAsync();
                    
                }
                catch (Exception x)
                {
                    Logger.LogError(x, "Gossip loop failed");
                }
            }
        }

        private async Task SendStateAsync()
        {
            //just make sure a cluster client cant send
            if (_pid == null)
            {
                return;
            }

            try
            {
                await _context.RequestAsync<SendGossipStateResponse>(_pid, new SendGossipStateRequest(), CancellationTokens.WithTimeout(5000));
            }
            catch (DeadLetterException)
            {
                
            }
            catch (OperationCanceledException)
            {
                
            }
            catch (Exception x)
            {
                //TODO: log
            }
        }

        internal Task ShutdownAsync()
        {
            Logger.LogInformation("Shutting down heartbeat");
            _context.Stop(_pid);
            Logger.LogInformation("Shut down heartbeat");
            return Task.CompletedTask;
        }
    }
}