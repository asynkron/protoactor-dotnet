using Proto;

namespace DependencyInjection
{
    public class ActorManager : IActorManager
    {
        private readonly IActorFactory actorFactory;

        public ActorManager(IActorFactory actorFactory)
        {
            this.actorFactory = actorFactory;
        }

        public void Activate()
        {
            actorFactory.GetActor<DIActor>().Tell(new DIActor.Ping("no-name"));
            actorFactory.GetActor<DIActor>("named").Tell(new DIActor.Ping("named"));
        }
    }
}