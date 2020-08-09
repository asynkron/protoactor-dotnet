// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Logging;
using static Proto.Cluster.Consul.Messages;

namespace Proto.Cluster.Consul
{
    public class ConsulClusterMonitor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<ConsulClusterMonitor>();

        private readonly ConsulProviderOptions _options;
        private readonly ConsulClient _client;

        private IMemberStatusValueSerializer _statusValueSerializer;
        private ActorSystem _system;

        public ConsulClusterMonitor(ActorSystem system, ConsulProviderOptions options, Action<ConsulClientConfiguration> consulConfig)
        {
            _system = system;
            _options = options;
            _client = new ConsulClient(consulConfig);
        }

        public async Task ReceiveAsync(IContext context)
        {
            var task = context.Message switch
            {
                RegisterMember cmd => Register(cmd, context),
                CheckStatus cmd => NotifyStatuses(cmd.Index, context.Self),
                DeregisterMember _ => UnregisterService(context),
                UpdateStatusValue cmd => RegisterService(cmd.StatusValue, context),
                ReregisterMember _ => RegisterService(_statusValue, context),
                Stopping _ => Stop(),
                _ => Task.CompletedTask
            };
            await task.ConfigureAwait(false);

            Task Stop()
            {
                Logger.LogDebug("Stopping monitoring for {Service}", _id);
                return _registered ? UnregisterService(context) : Actor.Done;
            }
        }

        private async Task Register(RegisterMember cmd, ISpawnerContext context)
        {
            _id = $"{cmd.ClusterName}@{cmd.Address}:{cmd.Port}";
            _clusterName = cmd.ClusterName;
            _address = cmd.Address;
            _port = cmd.Port;
            _kinds = cmd.Kinds;
            _statusValueSerializer = cmd.StatusValueSerializer;

            await RegisterService(cmd.StatusValue, context);
        }

        private Task RegisterService(IMemberStatusValue statusValue, ISpawnerContext context)
        {
            var registration = new AgentServiceRegistration
            {
                ID = _id,
                Name = _clusterName,
                Tags = _kinds,
                Address = _address,
                Port = _port,
                Meta = new Dictionary<string, string>
                {
                    {"StatusValue", _statusValueSerializer.Serialize(statusValue)},
                    {"DeregisterInterval", _options.DeregisterCritical.TotalSeconds.ToString(CultureInfo.InvariantCulture)}
                },
                Check = new AgentServiceCheck
                {
                    DeregisterCriticalServiceAfter = _options.DeregisterCritical,
                    TTL = _options.ServiceTtl
                }
            };

            Logger.LogDebug("Registering service {Service}", _id);

            _registered = true;
            _statusValue = statusValue;

            var ttlCheckProps = Props.FromProducer(() => new ConsulCheckin(_client, _id, _options.RefreshTtl));
            _ttlCheck = context.Spawn(ttlCheckProps);

            return _client.Agent.ServiceRegister(registration);
        }

        private Task UnregisterService(IStopperContext context)
        {
            Logger.LogDebug("Unregistering service {Service}", _id);

            _registered = false;
            context.Stop(_ttlCheck);

            return _client.Agent.ServiceDeregister(_id);
        }

        private async Task NotifyStatuses(ulong index, PID self)
        {
            var statuses = await _client.Health.Service(
                _clusterName, null, false, new QueryOptions
                {
                    WaitIndex = index,
                    WaitTime = _options.BlockingWaitTime
                }
            );

            Logger.LogDebug("Consul response: {@Response}", (object)statuses.Response);

            var reportedServices =
                statuses.Response
                    .Select(
                        x => new
                        {
                            Status = new MemberStatus(
                                x.Service.ID, x.Service.Address, x.Service.Port, x.Service.Tags,
                                x.Checks.All(c => c.Status.Status != "critical"),
                                _statusValueSerializer.Deserialize(x.Service.Meta["StatusValue"])
                            ),
                            DeregisterInterval = Parse(x.Service.Meta["DeregisterInterval"])
                        }
                    )
                    .ToList();

            var memberStatuses = reportedServices.Select(x => x.Status).ToList();

            // Update Tags (kinds) for this member
            _kinds = memberStatuses.FirstOrDefault(x => x.Address == _address && x.Port == _port)?.Kinds.ToArray();

            // Consul has the minimal deregister interval of 1 minute. Our default interval is 30 seconds, but Consul won't support it.
            // Therefore, we check if there are any members that fails on TTL check is recorded and when the specified interval expires,
            OverrideConsulDeregisterInterval();

            var res = new ClusterTopologyEvent(memberStatuses);
            _system.EventStream.Publish(res);

            _system.Root.Send(self, new CheckStatus { Index = statuses.LastIndex });

            void OverrideConsulDeregisterInterval()
            {
                var deadMembers = reportedServices
                    .Where(x => !x.Status.Alive && x.DeregisterInterval != default)
                    .Select(x => new DeadMember(x.Status.MemberId, DateTimeOffset.UtcNow, x.DeregisterInterval))
                    .ToList();

                if (deadMembers.Count == 0) return;

                // Remove members that have recovered
                _knownDeadMembers.RemoveAll(x => !deadMembers.Contains(x));

                // Find newly discovered dead members
                var newDeadMembers = deadMembers.Except(_knownDeadMembers);

                // Add newly discovered dead members to the list of known dead members.
                // Cannot use Concat/Distinct since we must keep the timestamp.
                _knownDeadMembers.AddRange(newDeadMembers);

                var reportMissing = _knownDeadMembers
                    .Where(x => DateTimeOffset.UtcNow > x.FoundDeadAt + x.DeregisterInterval)
                    .Select(x => x.MemberId)
                    .ToList();

                if (reportMissing.Count > 0)
                    Logger.LogInformation("Members to be excluded {@Dead}", reportMissing);

                memberStatuses.RemoveAll(x => reportMissing.Contains(x.MemberId));
            }

            static TimeSpan Parse(string metaInterval)
                => int.TryParse(metaInterval, out var seconds)
                    ? TimeSpan.FromSeconds(seconds)
                    : default;
        }

        private IMemberStatusValue _statusValue;
        private string _id;
        private string _clusterName;
        private string _address;
        private int _port;
        private string[] _kinds;
        private bool _registered;
        private PID _ttlCheck;

        private readonly List<DeadMember> _knownDeadMembers = new List<DeadMember>();

        private class DeadMember : IEquatable<DeadMember>
        {
            public DeadMember(string memberId, DateTimeOffset foundDeadAt, TimeSpan deregisterInterval)
            {
                MemberId = memberId;
                FoundDeadAt = foundDeadAt;
                DeregisterInterval = deregisterInterval;
            }

            public string MemberId { get; }
            public DateTimeOffset FoundDeadAt { get; }
            public TimeSpan DeregisterInterval { get; }

            public bool Equals(DeadMember other) => !ReferenceEquals(null, other) && (ReferenceEquals(this, other) || MemberId == other.MemberId);

            public override bool Equals(object obj)
                => !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((DeadMember)obj));

            public override int GetHashCode() => MemberId != null ? MemberId.GetHashCode() : 0;
        }
    }
}