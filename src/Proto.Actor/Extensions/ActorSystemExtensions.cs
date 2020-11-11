using System.Collections.Generic;

namespace Proto.Extensions
{
    public class ActorSystemExtensions
    {
        private readonly IActorSystemExtension[] _extensions = new IActorSystemExtension[100];
        private readonly ActorSystem _actorSystem;

        public ActorSystemExtensions(ActorSystem actorSystem)
        {
            _actorSystem = actorSystem;
        }

        public T GetExtension<T>() where T: IActorSystemExtension
        {
            var id = IActorSystemExtension<T>.Id;
            return (T)_extensions[id];
        }

        public void RegisterExtension<T>(IActorSystemExtension<T> extension) where T:IActorSystemExtension
        {
            var id = IActorSystemExtension<T>.Id;
            //TODO, ensure capacity
            _extensions[id] = extension;
        }
    }
}