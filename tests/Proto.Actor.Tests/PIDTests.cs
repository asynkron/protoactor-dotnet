using System;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class PIDTests 
    {


        [Fact]
        public async Task Given_ActorNotDead_Ref_ShouldReturnIt()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;
            var pid = Context.Spawn(Props.FromFunc(EmptyReceive));

            var p = pid.Ref(System);

            Assert.NotNull(p);
        }

        [Fact]
        public async Task Given_ActorDied_Ref_ShouldNotReturnIt()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var pid = Context.Spawn(Props.FromFunc(EmptyReceive).WithMailbox(() => new TestMailbox()));
            await Context.StopAsync(pid);

            var p = pid.Ref(System);

            Assert.Null(p);
        }

        [Fact]
        public async Task Given_OtherProcess_Ref_ShouldReturnIt()
        {
            await using var System = new ActorSystem();
            var Context = System.Root;

            var id = Guid.NewGuid().ToString();
            var p = new TestProcess(System);
            var (pid, _) = System.ProcessRegistry.TryAdd(id, p);

            var p2 = pid.Ref(System);

            Assert.Same(p, p2);
        }
    }
}