using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Proto
{
    public class Plugins
    {
        private ConcurrentDictionary<Type, IProtoPlugin> _plugins { get; } = new ConcurrentDictionary<Type, IProtoPlugin>();

        public bool AddPlugin<TPlugin>(TPlugin plugin)
            where TPlugin : class, IProtoPlugin
        {
            var pluginInterface = plugin.GetType().GetInterfaces().Single(p => p.IsAssignableFrom(typeof(IProtoPlugin)));
            return _plugins.TryAdd(pluginInterface, plugin);
        }

        public TPlugin GetPlugin<TPlugin>()
            where TPlugin : class, IProtoPlugin
        {
            var pluginInterface = typeof(TPlugin).GetInterfaces().Single(p => p.IsAssignableFrom(typeof(IProtoPlugin)));
            var instanceIsPresent = _plugins.TryGetValue(pluginInterface, out var foundInstance);
            return foundInstance is TPlugin plugin ? plugin : throw new PluginNotFoundException<TPlugin>();
        }
    }
}