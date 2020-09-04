using System;
using System.Threading.Tasks;
using ClusterExperimentInMemory.Messages;
using Proto;

namespace ClusterExperimentInMemory
{
    public class HelloActor : IActor
    {
        private static readonly Random rnd = new Random();
        public Task ReceiveAsync(IContext ctx)
        {

            switch (ctx.Message)
            {
                case Started _:
                    Console.Write("#");
                    ctx.SetReceiveTimeout(TimeSpan.FromSeconds(rnd.Next(20, 100)));
                    break;
                case HelloRequest _:
                    ctx.Respond(new HelloResponse());
                    break;
                case ReceiveTimeout _:
                    ctx.Stop(ctx.Self!);
                    break;
                case Stopped _:
                    Console.Write("T");
                    break;
                default:
                    break;
            }
            return Actor.Done;
        }
    }
}