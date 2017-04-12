using System;
using System.Collections.Generic;

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
    }
}
