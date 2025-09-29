using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Logging;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using Xunit;

namespace Proto.Remote.Tests;

public interface IRemoteFixture : IAsyncLifetime
{
    string RemoteAddress { get; }
    string RemoteAddress2 { get; }
    IRemote Remote { get; }
    ActorSystem ActorSystem { get; }
    IRemote ServerRemote1 { get; }
    LogStore LogStore { get; }
}

public abstract class RemoteFixture : IRemoteFixture
{
    protected enum RemoteTransportKind
    {
        GrpcNet,
        HostedGrpcNet,
        GrpcNetClient
    }

    protected sealed record RemoteEndpointDescriptor(RemoteTransportKind Kind, Func<RemoteConfig> ConfigFactory);

    protected sealed record RemoteFixtureDescriptor(
        RemoteEndpointDescriptor Client,
        RemoteEndpointDescriptor Server1,
        RemoteEndpointDescriptor Server2
    );

    private sealed record RemoteEndpoint(IRemote Remote, IHost Host);

    public static readonly Props EchoActorProps = Props.FromProducer(() => new EchoActor());

    private static readonly LogStore _logStore = new();

    private readonly RemoteEndpoint _clientEndpoint;
    private readonly ImmutableArray<RemoteEndpoint> _serverEndpoints;
    private readonly ImmutableArray<RemoteEndpoint> _allEndpoints;

    protected RemoteFixture(RemoteFixtureDescriptor descriptor)
    {
        _clientEndpoint = CreateEndpoint(descriptor.Client);
        Remote = _clientEndpoint.Remote;

        var server1 = CreateEndpoint(descriptor.Server1);
        var server2 = CreateEndpoint(descriptor.Server2);
        _serverEndpoints = ImmutableArray.Create(server1, server2);
        _allEndpoints = ImmutableArray.Create(_clientEndpoint, server1, server2);

        ServerRemote1 = server1.Remote;
        ServerRemote2 = server2.Remote;
    }

    public LogStore LogStore { get; } = _logStore;

    public string RemoteAddress => ServerRemote1.System.Address;
    public string RemoteAddress2 => ServerRemote2.System.Address;

    public IRemote Remote { get; }
    public ActorSystem ActorSystem => Remote.System;

    public IRemote ServerRemote1 { get; }
    public IRemote ServerRemote2 { get; }

    public virtual async Task InitializeAsync()
    {
        await Task.WhenAll(_serverEndpoints.Select(e => e.Remote.StartAsync()));
        await Remote.StartAsync();

        foreach (var endpoint in _serverEndpoints)
        {
            endpoint.Remote.System.Root.SpawnNamed(EchoActorProps, "EchoActorInstance");
        }
    }

    public virtual async Task DisposeAsync()
    {
        await Task.WhenAll(_allEndpoints.Select(endpoint => endpoint.Remote.ShutdownAsync()));

        foreach (var endpoint in _allEndpoints)
        {
            if (endpoint.Host is null)
            {
                continue;
            }

            await endpoint.Host.StopAsync();
            endpoint.Host.Dispose();
        }
    }

    protected static TRemoteConfig ConfigureServerRemoteConfig<TRemoteConfig>(TRemoteConfig serverRemoteConfig)
        where TRemoteConfig : RemoteConfig =>
        serverRemoteConfig
            .WithProtoMessages(Messages.ProtosReflection.Descriptor)
            .WithRemoteKinds(("EchoActor", EchoActorProps));

    protected static TRemoteConfig ConfigureClientRemoteConfig<TRemoteConfig>(TRemoteConfig clientRemoteConfig)
        where TRemoteConfig : RemoteConfig =>
        clientRemoteConfig
            .WithEndpointWriterMaxRetries(2)
            .WithEndpointWriterRetryBackOff(TimeSpan.FromMilliseconds(10))
            .WithEndpointWriterRetryTimeSpan(TimeSpan.FromSeconds(120))
            .WithProtoMessages(Messages.ProtosReflection.Descriptor)
            .WithRemoteKinds(("EchoActor", EchoActorProps));

    protected static (IHost, HostedGrpcNetRemote) GetHostedGrpcNetRemote(RemoteConfig config)
    {
        var hostBuilder = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices(services =>
                {
                    services.AddGrpc();
                    services.AddSingleton(Log.GetLoggerFactory());

                    services.AddSingleton(sp =>
                        {
                            var system = new ActorSystem();

                            system.Extensions.Register(new InstanceLogger(LogLevel.Debug, _logStore,
                                category: system.Id));

                            return system;
                        }
                    );

                    services.AddRemote(config);
                }
            )
            .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(kestrelServerOptions =>
                            {
                                kestrelServerOptions.Listen(IPAddress.Parse(config.Host), config.Port,
                                    listenOption => { listenOption.Protocols = HttpProtocols.Http2; }
                                );
                            }
                        )
                        .Configure(app =>
                            {
                                app.UseRouting();
                                app.UseProtoRemote();
                            }
                        );
                }
            );

        var host = hostBuilder.Start();

        return (host, host.Services.GetRequiredService<HostedGrpcNetRemote>());
    }

    protected static GrpcNetRemote GetGrpcNetRemote(RemoteConfig config)
    {
        return new GrpcNetRemote(new ActorSystem(), config);
    }

    protected static GrpcNetClientRemote GetGrpcNetClientRemote(RemoteConfig config)
    {
        return new GrpcNetClientRemote(new ActorSystem(), config);
    }

    protected static RemoteEndpointDescriptor Client(
        RemoteTransportKind transportKind,
        Func<RemoteConfig, RemoteConfig> configure = null
    ) => new(
        transportKind,
        () => ConfigureClientRemoteConfig(RemoteConfig.BindToLocalhost())
            .Apply(configure)
    );

    protected static RemoteEndpointDescriptor Server(
        RemoteTransportKind transportKind,
        Func<RemoteConfig, RemoteConfig> configure = null
    ) => new(
        transportKind,
        () => ConfigureServerRemoteConfig(RemoteConfig.BindToLocalhost())
            .Apply(configure)
    );

    protected static RemoteFixtureDescriptor Fixture(
        RemoteEndpointDescriptor client,
        RemoteEndpointDescriptor server1,
        RemoteEndpointDescriptor server2 = null
    ) => new(client, server1, server2 ?? server1);

    private static RemoteEndpoint CreateEndpoint(RemoteEndpointDescriptor descriptor)
    {
        var config = descriptor.ConfigFactory();

        return descriptor.Kind switch
        {
            RemoteTransportKind.GrpcNet => new RemoteEndpoint(GetGrpcNetRemote(config), null),
            RemoteTransportKind.GrpcNetClient => new RemoteEndpoint(GetGrpcNetClientRemote(config), null),
            RemoteTransportKind.HostedGrpcNet =>
                CreateHostedEndpoint(config),
            _ => throw new ArgumentOutOfRangeException(nameof(descriptor.Kind), descriptor.Kind, null)
        };
    }

    private static RemoteEndpoint CreateHostedEndpoint(RemoteConfig config)
    {
        var (host, remote) = GetHostedGrpcNetRemote(config);

        return new RemoteEndpoint(remote, host);
    }
}

internal static class RemoteConfigExtensions
{
    public static RemoteConfig Apply(
        this RemoteConfig config,
        Func<RemoteConfig, RemoteConfig> configure
    ) => configure is null
        ? config
        : configure(config);
}
