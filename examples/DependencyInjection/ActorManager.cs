using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;

namespace DependencyInjection
{
    public class ActorManager : IActorManager
    {
        private readonly IActorFactory actorFactory;
        private readonly Subscription<DIActor.Ping> subscription;

        public ActorManager(IActorFactory actorFactory, EventStream<DIActor.Ping> eventStream, ILogger<ActorManager> logger)
        {
            this.actorFactory = actorFactory;
            subscription = eventStream.Subscribe(x => logger.LogInformation($"EventStream reply: {x.Name}"));
        }

        public async Task ActivateAsync()
        {
            await actorFactory.GetActor<DIActor>().SendAsync(new DIActor.Ping("no-name"));
            await actorFactory.GetActor<DIActor>("named").SendAsync(new DIActor.Ping("named"));
        }
    }
}