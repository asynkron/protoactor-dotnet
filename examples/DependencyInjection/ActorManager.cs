using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Proto;

namespace DependencyInjection
{
    public class ActorManager : IActorManager
    {
        private readonly ActorSystem _system;
        private readonly IActorFactory _actorFactory;

        public ActorManager(IActorFactory actorFactory, ILogger<ActorManager> logger, ActorSystem system)
        {
            _actorFactory = actorFactory;
            _system = system;
            _system.EventStream.Subscribe<DIActor.Ping>(x => logger.LogInformation($"EventStream reply: {x.Name}"));
        }

        public void Activate()
        {
            _system.Root.Send(_actorFactory.GetActor<IDIActor>(), new DIActor.Ping("no-name-from-interface"));
            Thread.Sleep(TimeSpan.FromSeconds(1));
            _system.Root.Send(_actorFactory.GetActor<IDIActor>("named-from-interface"), new DIActor.Ping("named-from-interface"));
            Thread.Sleep(TimeSpan.FromSeconds(1));
            _system.Root.Send(_actorFactory.GetActor<DIActor>(), new DIActor.Ping("no-name"));
            Thread.Sleep(TimeSpan.FromSeconds(1));
            _system.Root.Send(_actorFactory.GetActor<DIActor>("named"), new DIActor.Ping("named"));
        }
    }
}