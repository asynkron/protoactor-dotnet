using System;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Remote.Tests
{
    public class RemoteFixture: IAsyncLifetime
    {
        private ProtoService _service;
        public const string RemoteAddress = "localhost:12000";

        public Remote Remote { get; private set; }
        public ActorSystem ActorSystem { get; private set; }
        
        public async Task InitializeAsync()
        {
            ActorSystem = new ActorSystem();

            var config =
                RemoteConfig.BindToLocalhost()
                    .WithEndpointWriterMaxRetries(2)
                    .WithEndpointWriterRetryBackOff(TimeSpan.FromMilliseconds(10))
                    .WithEndpointWriterRetryTimeSpan(TimeSpan.FromSeconds(120))
                    .WithProtoMessages(Messages.ProtosReflection.Descriptor);

            _service = new ProtoService(12000, "localhost");
            _service.StartAsync().Wait();

           Remote = new Remote(ActorSystem, config);
            await Remote.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await Remote.ShutdownAsync();
            await _service.StopAsync();
        }
    }
}