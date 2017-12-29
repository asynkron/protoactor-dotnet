using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Proto;
using Xunit;

namespace protobug
{
    public class SampleActor : IActor
    {
        public static bool Created = false;

        public SampleActor()
        {
            Created = true;
        }

        public Task ReceiveAsync(IContext context)
        {
            return Actor.Done;
        }
    }

    public class ActorFactoryTest
    {
        [Fact]
        public void SpawnActor()
        {
            var services = new ServiceCollection();
            services.AddProtoActor();

            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IActorFactory>();

            var pid = factory.GetActor<SampleActor>();

            pid.Tell("hello");
            pid.Stop();

            Assert.True(SampleActor.Created);
        }
    }
}
