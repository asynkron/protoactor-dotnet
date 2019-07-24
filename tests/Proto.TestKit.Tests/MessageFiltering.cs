using System;
using Xunit;

namespace Proto.TestKit.Tests
{
    [Collection("FactKitFacts"), Trait("Category", "FactKit")]
    public class MessageFiltering : TestKit
    {
        [Fact]
        public void CanRespondToAsyncRequest()
        {
            var a = CreateTestProbe();
            var b = CreateTestProbe();

            a.Context.AtLeastOnceDelivery(b, "hi", TimeSpan.FromMilliseconds(100));
            b.GetNextMessage<string>(x => x.Equals("hi"));
            b.Respond(new Confirmation());

            a.ExpectNoMessage();
            b.ExpectNoMessage();
        }

        [Fact]
        public void CanDeathWatch()
        {
            var a = CreateTestProbe();
            Context.Watch(a);
            Context.Stop(a);
            GetNextMessage<Terminated>();
        }

        [Fact]
        public void FishFailsWrongType()
        {
            for (var i = 0; i < 100; i++)
                Send(Probe, i);

            Send(Probe, "hi");

            for (var i = 0; i < 100; i++)
                Send(Probe, i);

            Assert.Throws<Exception>(() => FishForMessage<DateTime>());
        }

        [Fact]
        public void FishFailsMissing()
        {
            for (var i = 0; i < 100; i++)
                Send(Probe, i);

            Send(Probe, "hi");

            for (var i = 0; i < 100; i++)
                Send(Probe, i);

            Assert.Throws<Exception>(() => FishForMessage<string>(x => x.Equals("bye")));
        }

        [Fact]
        public void FishSucceedsType()
        {
            for (var i = 0; i < 100; i++)
                Send(Probe, i);

            Send(Probe, "hi");

            for (var i = 0; i < 100; i++)
                Send(Probe, i);

            FishForMessage<string>();
        }

        [Fact]
        public void FishSucceedsCondition()
        {
            for (var i = 0; i < 100; i++)
                Send(Probe, i);

            Send(Probe, "hi");

            for (var i = 0; i < 100; i++)
                Send(Probe, i);

            FishForMessage<string>(x => x.Equals("hi"));
        }

        [Fact]
        public void GetMovesToNextMessage()
        {
            for (var i = 0; i < 100; i++)
                Send(Probe, i);

            for (var i = 0; i < 100; i++)
                GetNextMessage<int>(x => x == i);
        }


        [Fact]
        public void GetSucceeds()
        {
            Send(Probe, "hi");
            GetNextMessage<string>();
        }

        [Fact]
        public void GetFailsWrongType()
        {
            Send(Probe, "hi");
            Assert.Throws<Exception>(() => GetNextMessage<DateTime>());
        }

        [Fact]
        public void GetFailsNoMessage()
        {
            Assert.Throws<Exception>(() => GetNextMessage<DateTime>());
        }

        [Fact]
        public void GetFailsCondition()
        {
            Send(Probe, "hi");
            Assert.Throws<Exception>(() => GetNextMessage<string>(x => x.Equals("bye")));
        }

        [Fact]
        public void ExpectNoMessageFails()
        {
            Send(Probe, "hi");
            Assert.Throws<Exception>(() => ExpectNoMessage());
        }

        [Fact]
        public void ExpectNoMessageSucceeds()
        {
            ExpectNoMessage();
        }
    }
}
