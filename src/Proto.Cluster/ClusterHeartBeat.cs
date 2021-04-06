// -----------------------------------------------------------------------
// <copyright file="ClusterHeartBeat.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster
{
    public record SetGossipStateKey(string Key, IMessage Value);

    public record SendGossipState();

    public class ClusterHeartBeatActor : IActor
    {
        private const string ClusterHeartBeatName = "ClusterHeartBeat";
        private GossipState _state = new();

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            SetGossipStateKey setState => OnSetGossipStateKey(context, setState),
            GossipState remoteState    => OnGossipState(context, remoteState),
            SendGossipState => OnSendGossipState(context),
            // HeartbeatRequest           => OnHeartbeatRequest(context),
            _                          => Task.CompletedTask
        };

        private async Task OnSendGossipState(IContext context) => await GossipMyState(context);

        // private static Task OnHeartbeatRequest(IContext context)
        // {
        //     context.Respond(new HeartbeatResponse
        //         {
        //             ActorCount = (uint) context.System.ProcessRegistry.ProcessCount
        //         }
        //     );
        //     return Task.CompletedTask;
        // }

        private async Task OnGossipState(IContext context, GossipState remoteState)
        {
            var (dirty,newState) = _state.MergeWith(remoteState);
            if (!dirty)
                return;
            
            //Console.WriteLine($"{context.System.Id} got new state: {newState} ... old state: {_state}");
            
            _state = newState;
            await GossipMyState(context);
        }

        private async Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            var memberId = context.System.Id;
            var (key, value) = setStateKey;
            var entry = _state.Entries.FirstOrDefault(e => e.Key == key && e.MemberId == memberId);

            if (entry == null)
            {
                entry = new GossipKeyValue
                {
                    MemberId = memberId,
                    Key = key,
                };
                _state.Entries.Add(entry);
            }

            entry.Version++;
            entry.Value = Any.Pack(value);

            await GossipMyState(context);

        }

        private async Task GossipMyState(IContext context)
        {
            var members = context.System.Cluster().MemberList.GetOtherMembers();

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
                    _context.Send(_pid, new SetGossipStateKey("heartbeat",new HeartbeatRequest()));
                }
                catch (Exception x)
                {
                   // _logger.LogError(x, "Heartbeat loop failed");
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