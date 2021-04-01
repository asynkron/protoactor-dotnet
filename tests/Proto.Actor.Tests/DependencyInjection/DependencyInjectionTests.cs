using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Proto.DependencyInjection;
using Xunit;

namespace Proto.Tests.DependencyInjection
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void CanResolveDependencies()
        {
            ServiceCollection s = new ServiceCollection();
            s.AddSingleton<FooDep>();
            s.AddSingleton<BarDep>();
            s.AddTransient<DiActor>();
            ServiceProvider provider = s.BuildServiceProvider();

            DependencyResolver resolver = new DependencyResolver(provider);
            DIExtension plugin = new DIExtension(resolver);

            ActorSystem system = new ActorSystem();
            system.Extensions.Register(plugin);

            Props props = system.DI().PropsFor<DiActor>();
            DiActor actor = (DiActor)props.Producer(system);

            Assert.NotNull(props);
            Assert.NotNull(actor);
            Assert.NotNull(actor.Bar);
            Assert.NotNull(actor.Foo);
        }

        public class FooDep
        {
        }

        public class BarDep
        {
        }

        public class DiActor : IActor
        {
            public DiActor(FooDep foo, BarDep bar)
            {
                Foo = foo;
                Bar = bar;
            }

            public BarDep Bar { get; }
            public FooDep Foo { get; }

            public Task ReceiveAsync(IContext context) => Task.CompletedTask;
        }
    }
}
