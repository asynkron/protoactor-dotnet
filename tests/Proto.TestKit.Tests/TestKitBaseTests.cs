using FluentAssertions;
using Xunit;

namespace Proto.TestKit.Tests
{
    public class TestKitBaseTests : TestKitBase
    {
        public TestKitBaseTests() => SetUp();
        
        [Fact]
        public void SenderIsSet()
        {
            Request(Probe, "hi");
            Sender.Should().BeNull();
            GetNextMessage<string>();
            Sender.Should().NotBeNull();
        }

        [Fact]
        public void SenderCorrectlyChanges()
        {
            var a = CreateTestProbe();
            var b = CreateTestProbe();

            a.Request(Probe, "hi");
            b.Request(Probe, "hi");
            Send(Probe, "hi");

            Sender.Should().BeNull();
            GetNextMessage<string>();
            Sender.Should().Be(a.Context.Self);
            GetNextMessage<string>();
            Sender.Should().Be(b.Context.Self);
            GetNextMessage<string>();
            Sender.Should().BeNull();
        }
    }
}