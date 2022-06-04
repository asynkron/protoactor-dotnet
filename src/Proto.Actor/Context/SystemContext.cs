// -----------------------------------------------------------------------
// <copyright file = "SystemContext.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Microsoft.Extensions.Logging;

namespace Proto;

public static class SystemContext
{
    private static readonly ILogger Logger = Log.CreateLogger(nameof(SystemContext));

    public static PID SpawnNamedSystem(this RootContext self, Props props, string name)
    {
        if (!name.StartsWith("$"))
        {
            Logger.LogError("SystemContext Failed to spawn system actor {Name}", name);
            throw new ArgumentException("System actor names must start with $", nameof(name));
        }
        
        try
        {
            var parent = props.GuardianStrategy is not null
                ? self.System.Guardians.GetGuardianPid(props.GuardianStrategy)
                : null;

            //augment props with system actor specific settings
            props = self.System.ConfigureSystemProps(name, props);
            return props.Spawn(self.System, name, parent);
        }
        catch (Exception x)
        {
            Logger.LogError(x, "SystemContext Failed to spawn system actor {Name}", name);
            throw;
        }
    }
}