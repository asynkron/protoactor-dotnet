using System;
using System.Threading.Tasks;
using Divergic.Logging.Xunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Remote.Tests
{
    public interface IRemoteFixture: IAsyncLifetime
    {
        string RemoteAddress { get; }
        IRemote Remote { get; }
        ActorSystem ActorSystem { get; }
        IRemote ServerRemote { get; }
    }

    public abstract class RemoteFixture : IRemoteFixture
    {
        public string RemoteAddress => ServerRemote.System.Address;

        public IRemote Remote { get; protected set; }
        public ActorSystem ActorSystem => Remote.System;

        public IRemote ServerRemote { get; protected set; }
        private static readonly Props EchoActorProps = Props.FromProducer(() => new EchoActor());

        protected static TRemoteConfig ConfigureServerRemoteConfig<TRemoteConfig>(TRemoteConfig serverRemoteConfig)
        where TRemoteConfig : RemoteConfigBase
        {
            return serverRemoteConfig
            .WithProtoMessages(Messages.ProtosReflection.Descriptor)
            .WithRemoteKinds(("EchoActor", EchoActorProps));
        }

        protected static TRemoteConfig ConfigureClientRemoteConfig<TRemoteConfig>(TRemoteConfig clientRemoteConfig)
        where TRemoteConfig : RemoteConfigBase
        {
            return clientRemoteConfig
            .WithEndpointWriterMaxRetries(2)
            .WithEndpointWriterRetryBackOff(TimeSpan.FromMilliseconds(10))
            .WithEndpointWriterRetryTimeSpan(TimeSpan.FromSeconds(120))
            .WithProtoMessages(Messages.ProtosReflection.Descriptor);
        }

        public virtual async Task InitializeAsync()
        {
            await ServerRemote.StartAsync();
            await Remote.StartAsync();
            ServerRemote.System.Root.SpawnNamed(EchoActorProps, "EchoActorInstance");
        }

        public virtual async Task DisposeAsync()
        {
            await Remote.ShutdownAsync();
            await ServerRemote.ShutdownAsync();
        }

        protected static (IHost, HostedGrpcNetRemote) GetHostedGrpcNetRemote(GrpcNetRemoteConfig config)
        {

#if NETCORE
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif
            var hostBuilder = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices(services =>
            {
                services.AddGrpc();
                services.AddSingleton<ILoggerFactory>(Log.GetLoggerFactory());
                services.AddSingleton(sp => new ActorSystem());
                services.AddRemote(config.WithAdvertisedHost("localhost"));
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel(kestrelServerOptions =>
                {
                    kestrelServerOptions.ListenAnyIP(config.Port, listenOption =>
                    {
                        listenOption.Protocols = HttpProtocols.Http2;
                    });
                })
                .Configure((app) =>
                {
                    app.UseRouting();
                    app.UseProtoRemote();
                });

            });
            var host = hostBuilder.Start();
            return (host, host.Services.GetRequiredService<HostedGrpcNetRemote>());
        }

        protected static GrpcCoreRemote GetGrpcCoreRemote(GrpcCoreRemoteConfig config)
        {
            return new GrpcCoreRemote(new ActorSystem(), config);
        }

        protected static GrpcNetRemote GetGrpcNetRemote(GrpcNetRemoteConfig config)
        {
#if NETCORE
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif
            return new GrpcNetRemote(new ActorSystem(), config);
        }
    }
}