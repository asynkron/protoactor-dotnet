// -----------------------------------------------------------------------
// <copyright file="OpenTelemetryMetricsTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.OpenTelemetry.Tests.Messages;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using Xunit;
using FluentAssertions;

namespace Proto.OpenTelemetry.Tests;

public class OpenTelemetryMetricsTests : IAsyncLifetime
{
    private Cluster.Cluster? _cluster;
    private TestExporter? _testExporter;
    private MeterProvider? _meterProvider;

    public async Task InitializeAsync()
    {
        _testExporter = new TestExporter();

        _meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddProtoActorInstrumentation()
            .AddReader(new BaseExportingMetricReader(_testExporter))
            .Build();

        _cluster = await StartCluster();
    }

    public async Task DisposeAsync()
    {
        await _cluster!.ShutdownAsync();
        _meterProvider!.Dispose();
    }

    [Fact]
    public async Task ReportsBasicMetrics()
    {
        await _cluster!.RequestAsync<Pong>("echo1", EchoActor.Kind, new Ping {Message = "hello"}, CancellationToken.None);
        await _cluster!.RequestAsync<Pong>("echo2", EchoActor.Kind, new Ping {Message = "hello"}, CancellationToken.None);

        _meterProvider.ForceFlush();

        var id = _cluster!.System.Id;
        var address = _cluster.System.Address;
        
       ShouldReportMetric("protocluster_virtualactor_requestasync_duration", "address", address, "clusterkind", EchoActor.Kind, "id", id, "messagetype", "Ping", "pidsource", "IIdentityLookup"); 
       ShouldReportMetric("protocluster_resolve_pid_duration", "address", address, "clusterkind", EchoActor.Kind, "id", id);
       ShouldReportMetric("protocluster_virtualactors", "address", address, "clusterkind", EchoActor.Kind, "id", id);
       ShouldReportMetric("protocluster_members_count", "address", address, "id", id);
       ShouldReportMetric("protoactor_actor_mailbox_length", "actortype", "EchoActor", "address", address, "id", id);
       ShouldReportMetric("protoactor_actor_messagereceive_duration", "actortype", "EchoActor", "address", address, "id", id, "messagetype", "Ping");
       ShouldReportMetric("protoactor_actor_spawn_count", "actortype", "EchoActor", "address", address, "id", id);
       ShouldReportMetric("protoactor_future_completed_count", "address", address, "id", id);
       ShouldReportMetric("protoactor_future_started_count", "address", address, "id", id);
       
       MetricShouldHaveValue("protocluster_members_count", 1, "address", address, "id", id);
       MetricShouldHaveValue("protocluster_virtualactors", 2, "address", address, "clusterkind", EchoActor.Kind, "id", id);
       MetricShouldHaveValue("protoactor_actor_spawn_count", 2, "actortype", "EchoActor", "address", address, "id", id);
    }

    private void ShouldReportMetric(string name, params string[] tagValues)
        => _testExporter!.AllExportedMetrics.Should().Contain(FormatMetricLogEntry(name, tagValues));

    private void MetricShouldHaveValue(string name, double value, params string[] tagValues)
        => _testExporter!.MetricValues[FormatMetricLogEntry(name, tagValues)].Should().Be(value);

    private async Task<Cluster.Cluster> StartCluster()
    {
        var system = new ActorSystem(ActorSystemConfig.Setup().WithMetrics());

        var cluster = system
            .WithRemote(
                GrpcNetRemoteConfig.BindToLocalhost().WithProtoMessages(MessagesReflection.Descriptor)
            )
            .WithCluster(
                ClusterConfig.Setup(
                        "metrics",
                        new TestProvider(new TestProviderOptions(), new InMemAgent()),
                        new PartitionIdentityLookup()
                    )
                    .WithClusterKind(new ClusterKind(EchoActor.Kind, EchoActor.Props))
            )
            .Cluster();

        await cluster.StartMemberAsync();
        return cluster;
    }

    private class EchoActor : IActor
    {
        public const string Kind = "echo";

        public static readonly Props Props = Props.FromProducer(() => new EchoActor());

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Ping ping:
                    var pong = new Pong {Message = "Pong: " + ping.Message};
                    context.Respond(pong);
                    break;
            }

            return Task.CompletedTask;
        }
    }

    private class TestExporter : BaseExporter<Metric>
    {
        public readonly HashSet<string> AllExportedMetrics = new();
        public readonly Dictionary<string, double> MetricValues = new();

        public override ExportResult Export(in Batch<Metric> batch)
        {
            // SuppressInstrumentationScope should be used to prevent exporter
            // code from generating telemetry and causing live-loop.
            using var scope = SuppressInstrumentationScope.Begin();

            foreach (var metric in batch)
            {
                foreach (var metricPoint in metric.GetMetricPoints())
                {
                    List<string> tagsValues = new();
                    var enumerator = metricPoint.Tags.GetEnumerator();

                    while (enumerator.MoveNext())
                    {
                        tagsValues.Add(enumerator.Current.Key);
                        tagsValues.Add(enumerator.Current.Value?.ToString() ?? "");
                    }

                    var metricEntry = FormatMetricLogEntry(metric.Name, tagsValues.ToArray());
                    
                    AllExportedMetrics.Add(metricEntry);

                    MetricValues[metricEntry] = metric.MetricType switch
                    {
                        MetricType.DoubleGauge => metricPoint.GetGaugeLastValueDouble(),
                        MetricType.LongGauge   => metricPoint.GetGaugeLastValueLong(),
                        MetricType.DoubleSum   => metricPoint.GetSumDouble(),
                        MetricType.LongSum     => metricPoint.GetSumLong(),
                        _                      => 0
                    };
                }
            }

            return ExportResult.Success;
        }


    }
    
    static string FormatMetricLogEntry(string name, params string[] tagValues)
    {
        if (tagValues.Length % 2 != 0)
            throw new ArgumentException("Provide tag and values names alternately", nameof(tagValues));

        var logEntry = name;

        for (var i = 0; i < tagValues.Length; i += 2)
        {
            logEntry += $"|{tagValues[i]}={tagValues[i + 1]}";
        }

        return logEntry;
    }
}