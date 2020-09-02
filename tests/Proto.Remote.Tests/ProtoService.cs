using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Proto.Remote.Tests
{
    public class ProtoService
    {
        private readonly ILogger<ProtoService> _logger;
        private readonly int _port;
        private readonly string _host;
        private IRemote _remote;

        public ProtoService(int port, string host)
        {
            ILogger<ProtoService> log = NullLogger<ProtoService>.Instance;
            _logger = log;
            _host = host;
            _port = port;
        }

        public Task StartAsync()
        {
            _logger.LogInformation("ProtoService starting on {Host}:{Port}...", _host, _port);

            var actorSystem = new ActorSystem();
            var serialization = new Serialization();
            _remote = new SelfHostedRemote(actorSystem, _port, remote =>
            {
                remote.Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
                remote.RemoteConfig.EndpointWriterOptions = new EndpointWriterOptions
                {
                    MaxRetries = 2,
                    RetryBackOffms = 10,
                    RetryTimeSpan = TimeSpan.FromSeconds(120)
                };
            });
            _remote.Start();

            var props = Props.FromProducer(() => new EchoActor(_host, _port));
            _remote.RemoteKindRegistry.RegisterKnownKind("EchoActor", props);
            actorSystem.Root.SpawnNamed(props, "EchoActorInstance");

            _logger.LogInformation("ProtoService started");

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _logger.LogInformation("ProtoService stopping...");
            return _remote.ShutdownAsync();
        }
    }
}