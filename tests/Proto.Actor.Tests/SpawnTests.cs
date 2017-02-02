using Xunit;
using static Proto.Tests.ActorFixture;

namespace Proto.Tests
{
    public class SpawnTests
    {
        [Fact]
        public void Given_PropsWithSpawner_SpawnShouldReturnPidCreatedBySpawner()
        {
            var spawnedPid = new PID("test", "test");
            var props = Actor.FromFunc(EmptyReceive)
                .WithSpawner((id, p, parent) => spawnedPid);

            var pid = Actor.Spawn(props);

            Assert.Same(spawnedPid, pid);
        }
    }
}
