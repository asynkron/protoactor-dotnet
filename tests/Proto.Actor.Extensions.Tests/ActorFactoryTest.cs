using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Proto;
using Xunit;
using System.IO;

namespace protobug
{
    public class SampleActor : IActor
    {
        public static bool Created = false;

        public SampleActor()
        {
            File.AppendAllText("Test.txt", "Create!" + Environment.NewLine);
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

            File.AppendAllText("Test.txt", pid.ToShortString() + Environment.NewLine);

            pid.Tell("hello");
            pid.Stop();

            Assert.True(SampleActor.Created);
        }
    }
}
