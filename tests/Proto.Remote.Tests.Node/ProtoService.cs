using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Proto.Remote.Tests.Node
{
    public class ProtoService : IHostedService
    {
        private readonly ILogger<ProtoService> _logger;
        private readonly int _port;
        private readonly string _host;

        public ProtoService(IConfiguration configuration, ILogger<ProtoService> logger, ILoggerFactory loggerFactory)
        {
            Log.SetLoggerFactory(loggerFactory);
            _logger = logger;
            _host = configuration["host"];
            _port = configuration.GetValue<int>("port");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProtoService starting on {Host}:{Port}...", _host, _port);
            
            var context = new RootContext();
            Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
            Remote.Start(_host, _port);

            var props = Props.FromProducer(() => new EchoActor(_host, _port));
            Remote.RegisterKnownKind("EchoActor", props);
            context.SpawnNamed(props, "EchoActorInstance");
            
            _logger.LogInformation("ProtoService started");
            
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ProtoService stopping...");
            return Remote.Shutdown();
        }
    }
}