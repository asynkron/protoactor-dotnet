using System.Collections.Generic;

namespace Proto.Extensions
{
    public class ActorSystemExtensions
    {
        private readonly List<IActorSystemExtension> _extensions = new List<IActorSystemExtension>();
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
            if (_extensions.Capacity <= extensionId)
            {
                _extensions.Capacity = extensionId;
            }

            _extensions[extensionId] = extension;
        }
    }
}