// -----------------------------------------------------------------------
// <copyright file="ClusterHeartBeat.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip
{
    public record GossipUpdate(string MemberId, string Key, Any Value, long SequenceNumber);
    public record GetGossipStateRequest(string Key);

    public record GetGossipStateResponse(ImmutableDictionary<string,Any> State);

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

        public async Task<ImmutableDictionary<string,T>> GetState<T>(string key) where T : IMessage, new()
        {
            _context.System.Logger()?.LogDebug("Gossiper getting state from {Pid}", _pid);

            var res = await _context.RequestAsync<GetGossipStateResponse>(_pid, new GetGossipStateRequest(key));

            var dict = res.State;
            var typed = ImmutableDictionary<string, T>.Empty;

            foreach (var (k, value) in dict)
            {
                typed = typed.SetItem(k, value.Unpack<T>());
            }
            
            return typed;
        }

        public void SetState(string key, IMessage value)
        {
            Logger.LogDebug("Gossiper setting state to {Pid}", _pid);
            _context.System.Logger()?.LogDebug("Gossiper setting state to {Pid}", _pid);
            if (_pid == null)
            {
                return;
            }
            
            _context.Send(_pid, new SetGossipStateKey(key, value));
        }

        internal Task StartAsync()
        {
            var props = Props.FromProducer(() => new GossipActor(_cluster.Config.GossipRequestTimeout));
            _pid = _context.SpawnNamed(props, GossipActorName);
            Logger.LogInformation("Started Cluster Gossip");
            _ = SafeTask.Run(GossipLoop);
            return Task.CompletedTask;
        }

        private async Task GossipLoop()
        {
            Logger.LogInformation("Starting gossip loop");
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
            catch (Exception)
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