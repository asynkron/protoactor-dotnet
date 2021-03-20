using System.Threading.Tasks;
using Proto;

namespace LocalPingPong
{
    public class PongActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case PingMsg msg:
                    context.Send(msg.Sender, new PongMsg());
                    break;
            }

            return Task.CompletedTask;
        }

        public static Props Props => Props.FromProducer(() => new PongActor());
    }
}