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

        public T GetExtension<T>(ActorSystemExtensionId<T> extensionId) where T: IActorSystemExtension
        {
            return (T)_extensions[extensionId.Id];
        }

        public void RegisterExtension<T>(ActorSystemExtensionId<T> extensionId, IActorSystemExtension extension) where T:IActorSystemExtension
        {
            //TODO, ensure capacity
            _extensions[extensionId.Id] = extension;
        }
    }
}