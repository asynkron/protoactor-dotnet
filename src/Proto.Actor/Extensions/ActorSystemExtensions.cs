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

        public T GetExtension<T>(int extensionId) where T: IActorSystemExtension
        {
            return (T)_extensions[extensionId];
        }

        public void RegisterExtension(int extensionId, IActorSystemExtension extension)
        {
            //TODO, ensure capacity
            _extensions[extensionId] = extension;
        }
    }
}