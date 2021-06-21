using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Logging;
using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using Xunit;

namespace Proto.Remote.Tests
{
    public interface IRemoteFixture : IAsyncLifetime
    {
        string RemoteAddress { get; }
        IRemote Remote { get; }
        ActorSystem ActorSystem { get; }
        IRemote ServerRemote { get; }
        LogStore LogStore { get; }
    }

    public abstract class RemoteFixture : IRemoteFixture
    {
        private static readonly Props EchoActorProps = Props.FromProducer(() => new EchoActor());

        private static LogStore _logStore = new();
        public LogStore LogStore { get; } = _logStore;
        
        public string RemoteAddress => ServerRemote.System.Address;

        public IRemote Remote { get; protected set; }
        public ActorSystem ActorSystem => Remote.System;

        public IRemote ServerRemote { get; protected set; }

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
                .WithProtoMessages(Messages.ProtosReflection.Descriptor);

        protected static (IHost, HostedGrpcNetRemote) GetHostedGrpcNetRemote(GrpcNetRemoteConfig config)
        {
#if NETCORE
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif
            var hostBuilder = Host.CreateDefaultBuilder(Array.Empty<string>())
                .ConfigureServices(services => {
                        services.AddGrpc();
                        services.AddSingleton(Log.GetLoggerFactory());
                        services.AddSingleton(sp => {
                                var system= new ActorSystem();
                                system.Extensions.Register(new InstanceLogger(LogLevel.Debug,_logStore,category:system.Id));
                                return system;
                            }
                        );
                        services.AddRemote(config);
                    }
                )
                .ConfigureWebHostDefaults(webBuilder => {
                        webBuilder.ConfigureKestrel(kestrelServerOptions => {
                                    kestrelServerOptions.Listen(IPAddress.Parse(config.Host), config.Port,
                                        listenOption => { listenOption.Protocols = HttpProtocols.Http2; }
                                    );
                                }
                            )
                            .Configure(app => {
                                    app.UseRouting();
                                    app.UseProtoRemote();
                                }
                            );
                    }
                );
            var host = hostBuilder.Start();
            return (host, host.Services.GetRequiredService<HostedGrpcNetRemote>());
        }

        protected static GrpcCoreRemote GetGrpcCoreRemote(GrpcCoreRemoteConfig config) =>
            new(new ActorSystem(), config);

        protected static GrpcNetRemote GetGrpcNetRemote(GrpcNetRemoteConfig config)
        {
#if NETCORE
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif
            return new GrpcNetRemote(new ActorSystem(), config);
        }
    }
}