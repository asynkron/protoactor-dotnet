using System;
using Proto.TestFixtures;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class PIDTests
    {
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;

        [Fact]
        public void Given_ActorNotDead_Ref_ShouldReturnIt()
        {
            PID pid = Context.Spawn(Props.FromFunc(EmptyReceive));

            Process? p = pid.Ref(System);

            Assert.NotNull(p);
        }

        [Fact]
        public async void Given_ActorDied_Ref_ShouldNotReturnIt()
        {
            PID pid = Context.Spawn(Props.FromFunc(EmptyReceive).WithMailbox(() => new TestMailbox()));
            await Context.StopAsync(pid);

            Process? p = pid.Ref(System);

            Assert.Null(p);
        }

        [Fact]
        public void Given_OtherProcess_Ref_ShouldReturnIt()
        {
            string id = Guid.NewGuid().ToString();
            TestProcess p = new TestProcess(System);
            (PID pid, _) = System.ProcessRegistry.TryAdd(id, p);

            Process? p2 = pid.Ref(System);

            Assert.Same(p, p2);
        }
    }
}
