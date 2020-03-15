using Xunit;

namespace Proto.TestKit.Tests
{
    [Collection("TestKitTests"), Trait("Category", "TestKit")]
    public class General : TestKit
    {

        [Fact]
        public void SenderIsSet()
        {
            Request(Probe, "hi");
            Assert.Null(Sender);
            GetNextMessage<string>();
            Assert.NotNull(Sender);
        }

        [Fact]
        public void SenderCorrectlyChanges()
        {
            var a = CreateTestProbe();
            var b = CreateTestProbe();

            a.Request(Probe, "hi");
            b.Request(Probe, "hi");
            Send(Probe, "hi");

            Assert.Null(Sender);
            GetNextMessage<string>();
            Assert.Equal(Sender, a.Context.Self);
            GetNextMessage<string>();
            Assert.Equal(Sender, b.Context.Self);
            GetNextMessage<string>();
            Assert.Null(Sender);
        }
    }
}