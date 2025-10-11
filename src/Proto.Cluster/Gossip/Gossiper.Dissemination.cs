using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Logging;

namespace Proto.Cluster.Gossip;

public partial class Gossiper
{
    internal Task StartGossipActorAsync(IGossip? gossip = null, IGossipTransport? transport = null)
    {
        _gossip = gossip ?? new Gossip(
            _systemId,
            _options.GossipFanout,
            _options.GossipMaxSend,
            _context.System.Logger(),
            _memberList.GetMembers,
            _options.GossipDebugLogging);

        var props = Props.FromProducer(() => new GossipActor(
            _options.GossipRequestTimeout,
            _gossip,
            transport ?? new GossipTransport(),
            _memberList,
            _blockList,
            _options.GossipDebugLogging));

        _pid = _context.SpawnNamedSystem(props, GossipActorName);
        _eventStream.Subscribe<ClusterTopology>(topology =>
        {
            var tmp = topology.Clone();
            tmp.Joined.Clear();
            tmp.Left.Clear();
            _context.Send(_pid, tmp);
        });

        return Task.CompletedTask;
    }

    internal Task StartgossipLoopAsync()
    {
        Logger.LogInformation("Started Cluster Gossip");
        _ = SafeTask.Run(GossipLoop);

        return Task.CompletedTask;
    }

    private async Task GossipLoop()
    {
        Logger.LogInformation("Gossip is waiting for cluster to join");
        await _joinedCluster;
        Logger.LogInformation("Starting gossip loop");
        await Task.Yield();

        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.GossipInterval).ConfigureAwait(false);

                await BlockExpiredHeartbeats().ConfigureAwait(false);

                await BlockGracefullyLeft().ConfigureAwait(false);

                await SetStateAsync(GossipKeys.Heartbeat, new MemberHeartbeat
                    {
                        ActorStatistics = GetActorStatistics()
                    }
                ).ConfigureAwait(false);

                await SendStateAsync().ConfigureAwait(false);
            }
            catch (DeadLetterException)
            {
                if (_shutdown.IsCancellationRequested)
                {
                    //pass. this is expected, system is shutting down
                }
                else
                {
                    Logger.LogError("Gossip loop failed, Gossip actor has stopped");
                }
            }
            catch (Exception x)
            {
                x.CheckFailFast();
                Logger.LogWarning(x, "Gossip loop failed");
            }
        }
    }

    private async Task SendStateAsync()
    {
        if (_pid == null)
        {
            return;
        }

        try
        {
            await _context.RequestAsync<SendGossipStateResponse>(_pid, new SendGossipStateRequest(),
                CancellationTokens.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (DeadLetterException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception x)
        {
            x.CheckFailFast();
        }
    }

    internal async Task ShutdownAsync()
    {
        if (_pid == null)
        {
            return;
        }

        Logger.LogInformation("Shutting down heartbeat");
        await _context.StopAsync(_pid).ConfigureAwait(false);
        Logger.LogInformation("Shut down heartbeat");
    }
}

