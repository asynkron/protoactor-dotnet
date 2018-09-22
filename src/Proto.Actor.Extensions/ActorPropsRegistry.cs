using System;
using System.Collections.Generic;
using System.Reflection;

namespace Proto
{
    public class ActorPropsRegistry
    {
        internal readonly Dictionary<Type, Func<Props, Props>> RegisteredProps = new Dictionary<Type, Func<Props, Props>>();

        internal ActorPropsRegistry()
        {
        }

        public void RegisterProps<T>(Func<Props, Props> props) where T : IActor
        {
            RegisteredProps.Add(typeof(T), props);
        }

        public void RegisterProps(Type actorType, Func<Props, Props> props)
        {
            if (!typeof(IActor).GetTypeInfo().IsAssignableFrom(actorType))
            {
                throw new InvalidOperationException($"Type {actorType.FullName} must implement {typeof(IActor).FullName}");
            }

            RegisteredProps.Add(actorType, props);
        }
    }
}
