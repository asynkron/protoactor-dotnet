// -----------------------------------------------------------------------
// <copyright file="ClusterHeartBeat.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster
{
    public record SetGossipState(string Key, Any value);
    public class ClusterHeartBeatActor : IActor
    {
        private GossipState _state = new();
        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            SetGossipState setState => OnSetGossipState(context, setState),
            GossipState remoteState => OnGossipState(remoteState),
            HeartbeatRequest        => OnHeartbeatRequest(context),
            _                       => Task.CompletedTask
        };

        private static Task OnHeartbeatRequest(IContext context)
        {
            context.Respond(new HeartbeatResponse
                {
                    ActorCount = (uint) context.System.ProcessRegistry.ProcessCount
                }
            );
            return Task.CompletedTask;
        }

        private Task OnGossipState(GossipState remoteState)
        {
            var newState = _state.MergeWith(remoteState);
            _state = newState;
            return Task.CompletedTask;
        }

        private Task OnSetGossipState(IContext context, SetGossipState setState)
        {
            var memberId = context.System.Id;
            var existing = _state.Entries.FirstOrDefault(e => e.Key == setState.Key && e.MemberId == memberId);

            if (existing != null)
            {
                existing.Version++;
                existing.Value = setState.value;
                return Task.CompletedTask;
            }

            var newEntry = new GossipKeyValue
            {
                MemberId = memberId,
                Key = setState.key,
                Version = 0,
                Value = setState.value
            };
            _state.Entries.Add(newEntry);
            return Task.CompletedTask;
        }
    }

    public class ClusterHeartBeat
    {
        private const string ClusterHeartBeatName = "ClusterHeartBeat";
        private readonly Cluster _cluster;
        private readonly RootContext _context;

        private ILogger _logger = null!;
        private PID _pid = null!;

        public ClusterHeartBeat(Cluster cluster)
        {
            _cluster = cluster;
            _context = _cluster.System.Root;
        }

        public Task StartAsync()
        {
            var props = Props.FromProducer(() => new ClusterHeartBeatActor());
            _pid = _context.SpawnNamed(props, ClusterHeartBeatName);
            _logger = Log.CreateLogger("ClusterHeartBeat-" + _cluster.LoggerId);
            _logger.LogInformation("Started Cluster Heartbeats");
            _ = SafeTask.Run(HeartBeatLoop);
            return Task.CompletedTask;
        }

        private async Task HeartBeatLoop()
        {
            await Task.Yield();

            while (!_cluster.System.Shutdown.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_cluster.Config.HeartBeatInterval);
                    var members = _cluster.MemberList.GetAllMembers();

                    foreach (var member in members)
                    {
                        var pid = PID.FromAddress(member.Address, ClusterHeartBeatName);

                        try
                        {
                            await _context.RequestAsync<HeartbeatResponse>(pid, new HeartbeatRequest(),
                                TimeSpan.FromSeconds(5)
                            );

                            _logger.LogDebug("Heartbeat request for member id {MemberId} Address {Address} succeeded",
                                member.Id, member.Address
                            );
                        }
                        catch (TimeoutException)
                        {
                            if (_cluster.System.Shutdown.IsCancellationRequested) return;

                            _logger.LogWarning("Heartbeat request for member id {MemberId} Address {Address} timed out",
                                member.Id, member.Address
                            );
                        }
                        catch (DeadLetterException)
                        {
                            if (_cluster.System.Shutdown.IsCancellationRequested) return;

                            _logger.LogWarning(
                                "Heartbeat request for member id {MemberId} Address {Address} got dead letter response",
                                member.Id, member.Address
                            );
                        }
                    }
                }
                catch (Exception x)
                {
                    _logger.LogError(x, "Heartbeat loop failed");
                }
            }
        }

        public Task ShutdownAsync()
        {
            _logger.LogInformation("Shutting down heartbeat");
            _context.Stop(_pid);
            _logger.LogInformation("Shut down heartbeat");
            return Task.CompletedTask;
        }
    }
}