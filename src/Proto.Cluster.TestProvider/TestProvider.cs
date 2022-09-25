// -----------------------------------------------------------------------
// <copyright file="TestProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Testing;

public class TestProvider : IClusterProvider
{
    private static readonly ILogger Logger = Log.CreateLogger<TestProvider>();
    private readonly InMemAgent _agent;
    private readonly TestProviderOptions _options;

    private string _id;
    private MemberList _memberList;
    private Timer _ttlReportTimer;

    public TestProvider(TestProviderOptions options, InMemAgent agent)
    {
        _options = options;
        _agent = agent;
    }

    public Task StartMemberAsync(Cluster cluster)
    {
        var memberList = cluster.MemberList;
        var (host, port) = cluster.System.GetAddress();
        var kinds = cluster.GetClusterKinds();
        _id = cluster.System.Id;
        _memberList = memberList;
        _agent.StatusUpdate += AgentOnStatusUpdate;
        StartTtlTimer();

        _agent.RegisterService(new AgentServiceRegistration
            {
                Host = host,
                ID = _id,
                Kinds = kinds,
                Port = port
            }
        );

        return Task.CompletedTask;
    }

    public Task StartClientAsync(Cluster cluster)
    {
        var memberList = cluster.MemberList;

        _id = cluster.System.Id;
        _memberList = memberList;
        _agent.StatusUpdate += AgentOnStatusUpdate;
        _agent.ForceUpdate();

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(bool graceful)
    {
        Logger.LogDebug("Unregistering service {Service}", _id);

        _ttlReportTimer?.Stop();
        _agent.DeregisterService(_id);

        return Task.CompletedTask;
    }

    private void AgentOnStatusUpdate(object sender, EventArgs e) => NotifyStatuses();

    private void NotifyStatuses()
    {
        var statuses = _agent.GetServicesHealth();

        Logger.LogDebug("TestAgent response: {@Response}", (object)statuses);

        var memberStatuses =
            statuses.Select(
                    x =>
                    {
                        var member = new Member
                        {
                            Id = x.ID,
                            Host = x.Host,
                            Port = x.Port
                        };

                        member.Kinds.AddRange(x.Kinds);

                        return member;
                    }
                )
                .ToList();

        _memberList.UpdateClusterTopology(memberStatuses);
    }

    private void StartTtlTimer()
    {
        _ttlReportTimer = new Timer(_options.RefreshTtl.TotalMilliseconds);
        _ttlReportTimer.Elapsed += (_, _) => RefreshTTL();
        _ttlReportTimer.Enabled = true;
        _ttlReportTimer.AutoReset = true;
        _ttlReportTimer.Start();
    }

    private void RefreshTTL() => _agent.RefreshServiceTTL(_id);
}