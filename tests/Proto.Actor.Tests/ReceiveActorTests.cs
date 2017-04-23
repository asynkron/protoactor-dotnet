using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public class ReceiveActorTests
    {
        [Fact]
        public void Routes_Message_To_Handler_For_Registered_Message_Type()
        {
            var PID = Actor.Spawn(new Props().WithProducer(() => new MyReceiveActor()));

            var response = PID.RequestAsync<string>("hello").Result;
            Assert.Equal("string handled", response);
        }

        [Fact]
        public void Calls_OnUnknownMessage_When_MessageType_Not_Registered()
        {
            var PID = Actor.Spawn(new Props().WithProducer(() => new MyReceiveActor()));

            var response = PID.RequestAsync<string>(new {}).Result;
            Assert.Equal("unknown handled", response);
        }
    }

    class MyReceiveActor : ReceiveActor
    {
        protected override Task OnUnknownMessage(IContext context)
        {
            context.Respond("unknown handled");
            return Actor.Done;
        }

        public MyReceiveActor()
        {
            RegisterHandler<string>(HandleString);
        }

        public Task HandleString(string message, IContext context)
        {
            context.Respond("string handled");
            return Actor.Done;
        }
    }
}
