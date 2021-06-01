using System;
using System.Threading.Tasks;
using Proto;

namespace LocalPingPong
{
    public class PongActor : IActor
    {
        private int messagesLeft = 1_000_000;

        public static Props Props => Props.FromProducer(() => new PongActor());

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case PingMsg msg:
                    messagesLeft--;

                    if (messagesLeft == 0) Console.Write("#");
                    else if (messagesLeft < 0) Console.Write("!"); //should not happen
                    context.Send(msg.Sender, new PongMsg());
                    break;
            }

            return Task.CompletedTask;
        }
    }
}