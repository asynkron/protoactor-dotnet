// -----------------------------------------------------------------------
//   <copyright file="Partition.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    public class Partition
    {
        private readonly Dictionary<string, PID> _kindMap = new Dictionary<string, PID>();

        private Subscription<object>? _memberStatusSub;
        private Cluster Cluster { get; }

        internal Partition(Cluster cluster) => Cluster = cluster;

        public void Setup(string[] kinds)
        {
            foreach (var kind in kinds)
            {
                var pid = SpawnPartitionActor(kind);
                _kindMap[kind] = pid;
            }

            _memberStatusSub = Cluster.System.EventStream.Subscribe<MemberStatusEvent>(
                msg =>
                {
                    foreach (var kind in msg.Kinds)
                    {
                        if (_kindMap.TryGetValue(kind, out var kindPid))
                        {
                            Cluster.System.Root.Send(kindPid, msg);
                        }
                    }
                }
            );
        }

        private PID SpawnPartitionActor(string kind)
        {
            var props = Props
                .FromProducer(() => new PartitionActor(Cluster, kind))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            var pid = Cluster.System.Root.SpawnNamed(props, "partition-" + kind);
            return pid;
        }

        public void Stop()
        {
            foreach (var kind in _kindMap.Values)
            {
                Cluster.System.Root.Stop(kind);
            }

            _kindMap.Clear();
            Cluster.System.EventStream.Unsubscribe(_memberStatusSub);
        }

        public static PID PartitionForKind(string address, string kind) => new PID(address, "partition-" + kind);
    }

    class PartitionActor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<PartitionActor>();

        private class SpawningProcess : TaskCompletionSource<ActorPidResponse>
        {
            public string SpawningAddress { get; }
            public SpawningProcess(string address) => SpawningAddress = address;
        }

        private readonly string _kind;
        private readonly Dictionary<string, PID> _partition = new Dictionary<string, PID>();                             //actor/grain name to PID
        private readonly Dictionary<PID, string> _reversePartition = new Dictionary<PID, string>();                      //PID to grain name
        private readonly Dictionary<string, SpawningProcess> _spawningProcs = new Dictionary<string, SpawningProcess>(); //spawning processes

        public Cluster Cluster { get; }

        public PartitionActor(Cluster cluster, string kind)
        {
            Cluster = cluster;
            _kind = kind;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    Logger.LogDebug("[Partition] Started for {Kind}", _kind);
                    break;
                case ActorPidRequest msg:
                    Spawn(msg, context);
                    break;
                case Terminated msg:
                    Terminated(msg);
                    break;
                case MemberJoinedEvent msg:
                    MemberJoined(msg, context);
                    break;
                case MemberRejoinedEvent msg:
                    MemberRejoined(msg, context);
                    break;
                case MemberLeftEvent msg:
                    MemberLeft(msg, context);
                    break;
            }

            return Actor.Done;
        }

        private void Terminated(Terminated msg)
        {
            //one of the actors we manage died, remove it from the lookup
            if (_reversePartition.TryGetValue(msg.Who, out var key))
            {
                Logger.LogDebug("[Partition] Removing terminated actor {Actor}", key);
                _partition.Remove(key);
                _reversePartition.Remove(msg.Who);
            }
        }

        private void MemberLeft(MemberLeftEvent memberLeft, IContext context)
        {
            Logger.LogInformation("[Partition] Kind {Kind} member left {Address}", _kind, memberLeft.Address);

            EnsureNewAddresses(context);

            // Process Spawning Process
            MakeUnavailable(memberLeft.Address);
        }

        private void MemberRejoined(MemberRejoinedEvent memberRejoined, IContext context)
        {
            Logger.LogInformation("[Partition] Kind {Kind} member rejoined {Address}", _kind, memberRejoined.Address);

            EnsureNewAddresses(context);

            // Process Spawning Process
            MakeUnavailable(memberRejoined.Address);
        }

        private void MakeUnavailable(string address)
        {
            foreach (var (_, sp) in _spawningProcs.Where(x => x.Value.SpawningAddress == address))
            {
                sp.TrySetResult(ActorPidResponse.Unavailable);
            }
        }

        private void MemberJoined(MemberJoinedEvent msg, IContext context)
        {
            Logger.LogInformation("[Partition] Kind {Kind} member joined {Address}", _kind, msg.Address);

            while (!Cluster.MemberList.IsMember(msg.Address)) { }

            EnsureNewAddresses(context);
            Cluster.PidCache.EnsureNewAddress(_kind);

            foreach (var (name, sp) in _spawningProcs)
            {
                var address = Cluster.MemberList.GetPartition(name, _kind);

                if (address != Cluster.System.ProcessRegistry.Address)
                {
                    sp.TrySetResult(ActorPidResponse.Unavailable);
                }
            }
        }

        private void EnsureNewAddresses(IContext context)
        {
            foreach (var (name, pid) in _partition.ToArray())
            {
                var address = Cluster.MemberList.GetPartition(name, _kind);

                if (address != pid.Address)
                {
                    RemoveActorsWithChangedAddress(name, pid, context);
                }
            }
        }

        private void RemoveActorsWithChangedAddress(string actorId, PID pid, IStopperContext context)
        {
            Logger.LogDebug("[Partition] Removing partition ownership for {Actor}", actorId);

            _partition.Remove(actorId);
            _reversePartition.Remove(pid);
        }

        private void Spawn(ActorPidRequest msg, IContext context)
        {
            //Check if exist in current partition dictionary
            if (_partition.TryGetValue(msg.Name, out var pid))
            {
                Logger.LogTrace("[Partition] Returning existing actor: {PID}", pid);
                context.Respond(new ActorPidResponse {Pid = pid});
                return;
            }

            //Check if is spawning, if so just await spawning finish.
            if (_spawningProcs.TryGetValue(msg.Name, out var spawning))
            {
                context.ReenterAfter(
                    spawning.Task,
                    rst =>
                    {
                        Logger.LogTrace("[Partition] Returning actor from the existing spawner: {PID}", pid);
                        context.Respond(rst.IsFaulted ? ActorPidResponse.Err : rst.Result);
                        return Actor.Done;
                    }
                );
                return;
            }

            //Get activator
            var activator = Cluster.MemberList.GetActivator(msg.Kind);

            if (string.IsNullOrEmpty(activator))
            {
                //No activator currently available, return unavailable
                Logger.LogDebug("[Partition] No members currently available");
                context.Respond(ActorPidResponse.Unavailable);
                return;
            }

            //Create SpawningProcess and cache it in spawning dictionary.
            spawning = new SpawningProcess(activator);
            _spawningProcs[msg.Name] = spawning;

            //Await SpawningProcess
            context.ReenterAfter(
                spawning.Task,
                rst =>
                {
                    _spawningProcs.Remove(msg.Name);

                    //Check if exist in current partition dictionary
                    //This is necessary to avoid race condition during partition map transfering.
                    if (_partition.TryGetValue(msg.Name, out pid))
                    {
                        Logger.LogTrace("[Partition] Returning existing actor: {PID}", pid);
                        context.Respond(new ActorPidResponse {Pid = pid});
                        return Actor.Done;
                    }

                    //Check if process is faulted
                    if (rst.IsFaulted)
                    {
                        Logger.LogWarning("[Partition] Spawning failed: {PID}", pid);
                        context.Respond(ActorPidResponse.Err);
                        return Actor.Done;
                    }

                    var pidResp = rst.Result;

                    if ((ResponseStatusCode) pidResp.StatusCode == ResponseStatusCode.OK)
                    {
                        pid = pidResp.Pid;
                        _partition[msg.Name] = pid;
                        _reversePartition[pid] = msg.Name;
                        context.Watch(pid);
                    }

                    Logger.LogTrace("[Partition] Returning newly spawned: {PID}", pid);
                    context.Respond(pidResp);
                    return Actor.Done;
                }
            );

            //Perform Spawning
            Task.Factory.StartNew(() => Spawning(msg, activator, 3, spawning));
        }

        private async Task Spawning(ActorPidRequest req, string activator, int retryCount, SpawningProcess spawning)
        {
            var retry = retryCount;
            ActorPidResponse pidResp;

            do
            {
                pidResp = await TrySpawn(retry == retryCount ? activator : Cluster.MemberList.GetActivator(req.Kind));
            } while ((ResponseStatusCode) pidResp.StatusCode == ResponseStatusCode.Unavailable && retry-- > 0);

            spawning.TrySetResult(pidResp);

            async Task<ActorPidResponse> TrySpawn(string act)
            {
                if (string.IsNullOrEmpty(act))
                {
                    //No activator currently available, return unavailable
                    Logger.LogDebug("[Partition] No activator currently available");
                    return ActorPidResponse.Unavailable;
                }

                try
                {
                    return await Cluster.Remote.SpawnNamedAsync(activator, req.Name, req.Kind, Cluster.Config!.TimeoutTimespan);
                }
                catch (TimeoutException)
                {
                    return ActorPidResponse.TimeOut;
                }
                catch
                {
                    return ActorPidResponse.Err;
                }
            }
        }
    }
}
