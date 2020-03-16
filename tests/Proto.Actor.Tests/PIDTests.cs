using System;
using Proto.TestFixtures;
using Xunit;

using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class PIDTests
    {
        private static readonly ActorSystem System = new ActorSystem();
        private static readonly RootContext Context = System.Root;
        [Fact]
        public void Given_ActorNotDead_Ref_ShouldReturnIt()
        {
            var pid = Context.Spawn(Props.FromFunc(EmptyReceive));

            var p = pid.Ref(System);

            Assert.NotNull(p);
        }

        [Fact]
        public async void Given_ActorDied_Ref_ShouldNotReturnIt()
        {
            var pid = Context.Spawn(Props.FromFunc(EmptyReceive).WithMailbox(() => new TestMailbox()));
            await Context.StopAsync(pid);

            var p = pid.Ref(System);

            Assert.Null(p);
        }

        [Fact]
        public void Given_OtherProcess_Ref_ShouldReturnIt()
        {
            var id = Guid.NewGuid().ToString();
            var p = new TestProcess(System);
            var (pid, _) = System.ProcessRegistry.TryAdd(id, p);

            var p2 = pid.Ref(System);

            Assert.Same(p, p2);
        }
    }
}
