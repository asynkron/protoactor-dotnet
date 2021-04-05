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
    public record SetGossipStateKey(string Key, Any Value);

    public class ClusterHeartBeatActor : IActor
    {
        private const string ClusterHeartBeatName = "ClusterHeartBeat";
        private GossipState _state = new();

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            SetGossipStateKey setState => OnSetGossipStateKey(context, setState),
            GossipState remoteState    => OnGossipState(context, remoteState),
            HeartbeatRequest           => OnHeartbeatRequest(context),
            _                          => Task.CompletedTask
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

        private async Task OnGossipState(IContext context, GossipState remoteState)
        {
            var newState = _state.MergeWith(remoteState);
            _state = newState;
            //TODO: only do if state changed
            await GossipMyState(context);
        }

        private async Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            var memberId = context.System.Id;
            var entry = _state.Entries.FirstOrDefault(e => e.Key == setStateKey.Key && e.MemberId == memberId);

            if (entry == null)
            {
                entry = new GossipKeyValue
                {
                    MemberId = memberId,
                    Key = setStateKey.Key,
                };
                _state.Entries.Add(entry);
            }

            entry.Version++;
            entry.Value = setStateKey.Value;

            await GossipMyState(context);

        }

        private async Task GossipMyState(IContext context)
        {
            var members = context.System.Cluster().MemberList.GetAllMembers();

            var rnd = new Random();
            var gossipToMembers =
                members
                    .Select(m => (member:m, index:rnd.Next()))
                    .OrderBy(m => m.index)
                    .Take(3)
                    .Select(m => m.member)
                    .ToList();

            foreach (var member in gossipToMembers)
            {
                var pid = PID.FromAddress(member.Address, ClusterHeartBeatName);
                context.Send(pid, _state);
            }
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