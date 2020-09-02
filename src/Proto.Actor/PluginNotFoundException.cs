// -----------------------------------------------------------------------
//   <copyright file="PluginNotFoundException.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto
{
    public class PluginNotFoundException<TPlugin> : Exception
    where TPlugin : IProtoPlugin
    {
        public PluginNotFoundException() : base($"Plugin {typeof(TPlugin).Name} not found") { }
    }
}