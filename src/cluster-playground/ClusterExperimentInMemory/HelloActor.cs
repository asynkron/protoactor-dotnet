using System;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using Proto;

namespace ClusterExperiment1
{
    public class HelloActor : IActor
    {
        public Task ReceiveAsync(IContext ctx)
        {

            switch (ctx.Message)
            {
                case Started _:
                    Console.Write("#");
                    ctx.SetReceiveTimeout(TimeSpan.FromSeconds(20));
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