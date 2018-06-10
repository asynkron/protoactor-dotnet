using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace Proto.ActorExtensions.Tests
{
    public class ActorFactoryTest
    {
        [Fact]
        public async void SpawnActor()
        {
            var context = new RootContext();
            var services = new ServiceCollection();
            services.AddProtoActor();

            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IActorFactory>();

            var pid = factory.GetActor<SampleActor>();

            context.Send(pid, "hello");

            await pid.StopAsync();

            Assert.True(SampleActor.Created);
        }

        [Fact]
        public async void should_register_by_type()
        {
            var services = new ServiceCollection();
            var created = false;

            Func<IActor> producer = () =>
            {
                created = true;
                return new SampleActor();
            };

            services.AddProtoActor(register => register.RegisterProps(typeof(SampleActor), p => p.WithProducer(producer)));

            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IActorFactory>();

            var pid = factory.GetActor<SampleActor>();

            await pid.StopAsync();

            Assert.True(created);
        }

        [Fact]
        public void should_throw_if_not_actor_type()
        {
            var services = new ServiceCollection();
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                services.AddProtoActor(register => register.RegisterProps(GetType(), p => p));
            });

            Assert.Equal($"Type {GetType().FullName} must implement {typeof(IActor).FullName}", ex.Message);
        }
    }
}
