// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;

namespace Proto.Logging
{
    [PublicAPI]
    public static class Extensions
    {
        public static InstanceLogger? Logger(this IContext context) => context.System.Logger();
        public static InstanceLogger? Logger(this ActorSystem system) => system.Extensions.Get<InstanceLogger>();
        
    }
}