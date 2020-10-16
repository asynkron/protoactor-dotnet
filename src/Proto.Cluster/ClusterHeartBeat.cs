using System;
using System.Threading;
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
                context.Respond(new HeartbeatResponse());
            }
            
            return Task.CompletedTask;
        }
    }
    public class ClusterHeartBeat
    {
        private readonly Cluster _cluster;
        private ILogger _logger = null!;
        private PID _pid = null!;
        private const string ClusterHeartBeatName = "ClusterHeartBeat"; 
        private readonly CancellationTokenSource _ct = new CancellationTokenSource();
        private readonly ActorSystem _system;
        private readonly RootContext _context;

        public ClusterHeartBeat(Cluster cluster)
        {
            _cluster = cluster;
            _system = _cluster.System;
            _context = _system.Root;
        }

        public Task StartAsync()
        {
            var props = Props.FromProducer(() => new ClusterHeartBeatActor());
            _pid = _context.SpawnNamed(props, ClusterHeartBeatName);
            _logger = Log.CreateLogger("ClusterHeartBeat-" + _cluster.LoggerId);
            _logger.LogInformation("Started Cluster Heartbeats");
            _ = Task.Run(HeartBeatLoop);
            return Task.CompletedTask;
        }

        private async Task HeartBeatLoop()
        {
            await Task.Yield();
            while (!_ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_cluster.Config.HeartBeatInterval);
                    var members = _cluster.MemberList.GetAllMembers();

                    foreach (var member in members)
                    {
                        var pid = PID.FromAddress(member.Address,ClusterHeartBeatName);
                        
                        try
                        {
                            await _context.RequestAsync<HeartbeatResponse>(pid, new HeartbeatRequest(),
                                TimeSpan.FromSeconds(5)
                            );
                            
                            _logger.LogInformation("Heartbeat request for member id {MemberId} Address {Address} succeeded",member.Id,member.Address);
                        }
                        catch (TimeoutException)
                        {
                            _logger.LogWarning("Heartbeat request for member id {MemberId} Address {Address} timed out",member.Id,member.Address);
                        }
                    }
                }
                catch (Exception x)
                {
                    _logger.LogError(x,"Heartbeat loop failed");
                }
            }
        }

        public Task ShutdownAsync()
        {
            _context.Stop(_pid);
            _ct.Cancel();
            return Task.CompletedTask;
        }
    }
}