using System;

namespace Proto
{
    public class PluginNotFoundException<TPlugin> : Exception
    where TPlugin : IProtoPlugin
    {
        public PluginNotFoundException() : base($"Plugin {typeof(TPlugin).Name} not found") { }
    }
}