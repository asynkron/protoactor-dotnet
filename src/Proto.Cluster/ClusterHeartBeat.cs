// -----------------------------------------------------------------------
// <copyright file="ClusterHeartBeat.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster
{
    public class ClusterHeartBeatActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is HeartbeatRequest)
            {
                context.Respond(new HeartbeatResponse
                    {
                        ActorCount = (uint) context.System.ProcessRegistry.ProcessCount
                    }
                );
            }

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