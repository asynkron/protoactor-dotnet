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
            var services = new ServiceCollection();
            services.AddProtoActor();

            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IActorFactory>();
            var system = provider.GetRequiredService<ActorSystem>();

            var pid = factory.GetActor<SampleActor>();

            system.Root.Send(pid, "hello");

            await system.Root.StopAsync(pid);

            Assert.True(SampleActor.Created);
        }

        [Fact]
        public async void SpawnActorFromInterface()
        {
            var services = new ServiceCollection();
            services.AddProtoActor();
            services.AddTransient<ISampleActor, SampleActor>();

            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IActorFactory>();
            var system = provider.GetRequiredService<ActorSystem>();

            var pid = factory.GetActor<ISampleActor>();

            system.Root.Send(pid, "hello");

            await system.Root.StopAsync(pid);

            Assert.True(SampleActor.Created);
        }

        [Fact]
        public async void should_register_by_type()
        {
            var services = new ServiceCollection();
            var created = false;

            IActor Producer()
            {
                created = true;
                return new SampleActor();
            }

            services.AddProtoActor(register => register.RegisterProps(typeof(SampleActor), p => p.WithProducer(Producer)));

            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IActorFactory>();
            var system = provider.GetRequiredService<ActorSystem>();

            var pid = factory.GetActor<SampleActor>();

            await system.Root.StopAsync(pid);

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
