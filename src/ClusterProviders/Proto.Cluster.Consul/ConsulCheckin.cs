using System;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Logging;
using static Proto.Cluster.Consul.Messages;

namespace Proto.Cluster.Consul
{
    public class ConsulCheckin : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<ConsulCheckin>();

        private readonly ConsulClient _client;
        private readonly string _id;
        private readonly TimeSpan _period;

        public ConsulCheckin(ConsulClient client, string id, TimeSpan period)
        {
            _client = client;
            _id = id;
            _period = period;
        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                Started _   => Start(),
                UpdateTtl _ => Task.Delay(_period).ContinueWith(_ => ReportAliveToConsul()),
                _           => Actor.Done
            };

            Task Start()
            {
                context.Send(context.Self, new UpdateTtl());
                return Actor.Done;
            }

            async Task ReportAliveToConsul()
            {
                Logger.LogTrace("[ConsulProvider] Confirming alive for {Service}", _id);
                context.Send(context.Self, new UpdateTtl());

                try
                {
                    await _client.Agent.PassTTL("service:" + _id, "");
                }
                catch (ConsulRequestException e) when (e.Message.Contains("does not have associated TTL"))
                {
                    // Such an exception happens when the service was unregistered but then woke up,
                    // so we need to re-register it and restart the alive reporting.
                    context.Poison(context.Self);
                    context.Send(context.Parent, new ReregisterMember());
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Error occured while reporting {Service} alive to Consul", _id);
                    throw;
                }
            }
        }
    }
}