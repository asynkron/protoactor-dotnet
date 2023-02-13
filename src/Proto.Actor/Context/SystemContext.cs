// -----------------------------------------------------------------------
// <copyright file = "SystemContext.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Microsoft.Extensions.Logging;

namespace Proto;

public static partial class SystemContext
{
    private static readonly ILogger Logger = Log.CreateLogger(nameof(SystemContext));

    /// <summary>
    ///     Spawns a system actor with the given name.
    /// </summary>
    /// <param name="self"></param>
    /// <param name="props">Props of the actor</param>
    /// <param name="name">Name of the actor (must start with $)</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static PID SpawnNamedSystem(this IRootContext self, Props props, string name)
    {
        if (!name.StartsWith("$"))
        {
            var ex = new ArgumentException("System actor names must start with $", nameof(name));
            Logger.LogFailedToSpawnActor(ex, name);

            throw ex;
        }

        try
        {
            var parent = props.GuardianStrategy is not null
                ? self.System.Guardians.GetGuardianPid(props.GuardianStrategy)
                : null;

            return props.SpawnSystem(self.System, name, parent);
        }
        catch (Exception x)
        {
            Logger.LogFailedToSpawnActor(x, name);

            throw;
        }
    }

    [LoggerMessage(0, LogLevel.Error, "SystemContext Failed to spawn system actor {Name}")]
    static partial void LogFailedToSpawnActor(this ILogger logger, Exception ex, string name);
}