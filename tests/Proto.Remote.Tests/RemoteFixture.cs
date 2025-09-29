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

    protected sealed record RemoteEndpointDescriptor(RemoteTransportKind Kind, Func<GrpcNetRemoteConfig> ConfigFactory);

    protected sealed record RemoteFixtureDescriptor(
        RemoteEndpointDescriptor Client,
        RemoteEndpointDescriptor Server1,
        RemoteEndpointDescriptor Server2
    );

    private sealed record RemoteEndpoint(IRemote Remote, IHost Host);

    public static readonly Props EchoActorProps = Props.FromProducer(() => new EchoActor());

    private static readonly LogStore LogStoreInstance = new();

    private readonly RemoteEndpoint _clientEndpoint;
    private readonly ImmutableArray<RemoteEndpoint> _serverEndpoints;
    private readonly ImmutableArray<RemoteEndpoint> _allEndpoints;

    protected RemoteFixture(RemoteFixtureDescriptor descriptor)
    {
        _clientEndpoint = CreateEndpoint(descriptor.Client);
        Remote = _clientEndpoint.Remote;

        var server1 = CreateEndpoint(descriptor.Server1);
        var server2 = CreateEndpoint(descriptor.Server2);

        ServerRemote1 = server1.Remote;
        ServerRemote2 = server2.Remote;
        _serverEndpoints = ImmutableArray.Create(server1, server2);
        _allEndpoints = ImmutableArray.Create(_clientEndpoint, server1, server2);
    }

    public LogStore LogStore { get; } = LogStoreInstance;

    public string RemoteAddress => ServerRemote1.System.Address;
    public string RemoteAddress2 => ServerRemote2.System.Address;

    public IRemote Remote { get; }
    public ActorSystem ActorSystem => Remote.System;

    public IRemote ServerRemote1 { get; }
    public IRemote ServerRemote2 { get; }

    public virtual async Task InitializeAsync()
    {
        await Task.WhenAll(_serverEndpoints.Select(endpoint => endpoint.Remote.StartAsync()));
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
            if (endpoint.Host == null)
            {
                continue;
            }

            await endpoint.Host.StopAsync();
            endpoint.Host.Dispose();
        }
    }

    protected static TRemoteConfig ConfigureServerRemoteConfig<TRemoteConfig>(TRemoteConfig serverRemoteConfig)
        where TRemoteConfig : RemoteConfigBase =>
        serverRemoteConfig
            .WithProtoMessages(Messages.ProtosReflection.Descriptor)
            .WithRemoteKinds(("EchoActor", EchoActorProps));

    protected static TRemoteConfig ConfigureClientRemoteConfig<TRemoteConfig>(TRemoteConfig clientRemoteConfig)
        where TRemoteConfig : RemoteConfigBase =>
        clientRemoteConfig
            .WithEndpointWriterMaxRetries(2)
            .WithEndpointWriterRetryBackOff(TimeSpan.FromMilliseconds(10))
            .WithEndpointWriterRetryTimeSpan(TimeSpan.FromSeconds(120))
            .WithProtoMessages(Messages.ProtosReflection.Descriptor)
            .WithRemoteKinds(("EchoActor", EchoActorProps));

    protected static (IHost, HostedGrpcNetRemote) GetHostedGrpcNetRemote(GrpcNetRemoteConfig config)
    {
#if NETCOREAPP3_1
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif
        var hostBuilder = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices(services =>
                {
                    services.AddGrpc();
                    services.AddSingleton(Log.GetLoggerFactory());
                    services.AddSingleton(_ =>
                        {
                            var system = new ActorSystem();
                            system.Extensions.Register(new InstanceLogger(LogLevel.Debug, LogStoreInstance,
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
                    ).Configure(app =>
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
        

    protected static GrpcNetRemote GetGrpcNetRemote(GrpcNetRemoteConfig config)
    {
#if NETCOREAPP3_1
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif
        return new GrpcNetRemote(new ActorSystem(), config);
    }

    protected static GrpcNetClientRemote GetGrpcNetClientRemote(GrpcNetRemoteConfig config)
    {
#if NETCOREAPP3_1
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif
        return new GrpcNetClientRemote(new ActorSystem(), config);
    }

    protected static RemoteEndpointDescriptor Client(
        RemoteTransportKind transportKind,
        Func<GrpcNetRemoteConfig, GrpcNetRemoteConfig> configure = null
    ) => new(
        transportKind,
        () => ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost())
            .Apply(configure)
    );

    protected static RemoteEndpointDescriptor Server(
        RemoteTransportKind transportKind,
        Func<GrpcNetRemoteConfig, GrpcNetRemoteConfig> configure = null
    ) => new(
        transportKind,
        () => ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost())
            .Apply(configure)
    );

    protected static RemoteFixtureDescriptor FixtureDescriptor(
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
            RemoteTransportKind.HostedGrpcNet => CreateHostedEndpoint(config),
            _ => throw new ArgumentOutOfRangeException(nameof(descriptor.Kind), descriptor.Kind, null)
        };
    }

    private static RemoteEndpoint CreateHostedEndpoint(GrpcNetRemoteConfig config)
    {
        var (host, remote) = GetHostedGrpcNetRemote(config);

        return new RemoteEndpoint(remote, host);
    }
}

internal static class GrpcNetRemoteConfigExtensions
{
    public static GrpcNetRemoteConfig Apply(
        this GrpcNetRemoteConfig config,
        Func<GrpcNetRemoteConfig, GrpcNetRemoteConfig> configure
    ) => configure == null
        ? config
        : configure(config);
}
