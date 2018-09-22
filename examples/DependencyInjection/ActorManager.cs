using Microsoft.Extensions.Logging;
using Proto;

namespace DependencyInjection
{
    public class ActorManager : IActorManager
    {
        private readonly RootContext _context = new RootContext();
        private readonly IActorFactory _actorFactory;

        public ActorManager(IActorFactory actorFactory, ILogger<ActorManager> logger)
        {
            _actorFactory = actorFactory;
            EventStream.Instance.Subscribe<DIActor.Ping>(x => logger.LogInformation($"EventStream reply: {x.Name}"));
        }

        public void Activate()
        {
            _context.Send( _actorFactory.GetActor<DIActor>(), new DIActor.Ping("no-name"));
            _context.Send(_actorFactory.GetActor<DIActor>("named"), new DIActor.Ping("named"));
        }
    }
}