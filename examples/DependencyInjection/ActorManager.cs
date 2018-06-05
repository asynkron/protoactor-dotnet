using Microsoft.Extensions.Logging;
using Proto;

namespace DependencyInjection
{
    public class ActorManager : IActorManager
    {
        private readonly IActorFactory actorFactory;

        public ActorManager(IActorFactory actorFactory, ILogger<ActorManager> logger)
        {
            this.actorFactory = actorFactory;
            EventStream.Instance.Subscribe<DIActor.Ping>(x => logger.LogInformation($"EventStream reply: {x.Name}"));
        }

        public void Activate()
        {
            RootContext.DefaultContext.Send( actorFactory.GetActor<DIActor>(), new DIActor.Ping("no-name"));
            RootContext.DefaultContext.Send(actorFactory.GetActor<DIActor>("named"), new DIActor.Ping("named"));
        }
    }
}