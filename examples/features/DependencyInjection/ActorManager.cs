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

        public void Activate()
        {
            actorFactory.GetActor<DIActor>().Tell(new DIActor.Ping("no-name"));
            actorFactory.GetActor<DIActor>("named").Tell(new DIActor.Ping("named"));
        }
    }
}