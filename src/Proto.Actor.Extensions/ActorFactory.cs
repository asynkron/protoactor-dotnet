using System;
using Microsoft.Extensions.DependencyInjection;

namespace Proto
{
    public class ActorFactory : IActorFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ActorPropsRegistry _actorPropsRegistry;

        public ActorFactory(IServiceProvider serviceProvider, ActorPropsRegistry actorPropsRegistry)
        {
            _serviceProvider = serviceProvider;
            _actorPropsRegistry = actorPropsRegistry;
        }

        public PID RegisterActor<T>(T actor, string id = null, string address = null, IContext parent = null)
            where T : IActor
        {
            id = id ?? typeof(T).FullName;
            return GetActor(id, address, parent, () => CreateActor<T>(id, parent, () => new Props().WithProducer(() => actor)));
        }

        public PID GetActor(string id, string address = null, IContext parent = null)
        {
            return GetActor(id, address, parent, () => throw new InvalidOperationException($"Actor not created {id}"));
        }

        public PID GetActor<T>(string id = null, string address = null, IContext parent = null)
            where T : IActor
        {
            id = id ?? typeof(T).FullName;
            return GetActor(id, address, parent, () => CreateActor<T>(id, parent, () => new Props().WithProducer(() => ActivatorUtilities.CreateInstance<T>(_serviceProvider))));
        }

        public PID GetActor(string id, string address, IContext parent, Func<PID> create)
        {
            address = address ?? "nonhost";

            var pidId = id;
            if (parent != null)
            {
                pidId = $"{parent.Self.Id}/{id}";
            }

            var pid = new PID(address, pidId);
            var reff = ProcessRegistry.Instance.Get(pid);
            if (reff is DeadLetterProcess)
            {
                pid = create();
            }
            return pid;
        }

        private PID CreateActor<T>(string id, IContext parent, Func<Props> producer)
            where T : IActor
        {
            if (!_actorPropsRegistry.RegisteredProps.TryGetValue(typeof(T), out var props))
            {
                props = x => x;
            }

            var props2 = props(producer());
            if (parent == null)
            {
                return RootContext.Empty.SpawnNamed(props2, id);
            }
            return parent.SpawnNamed(props2, id);
        }
    }
}