// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Mailbox;
using Proto.Utils;

namespace Proto;

public static class UtilExtensions
{
    internal static void SendSystemMessage(this IEnumerable<PID> self, SystemMessage message, ActorSystem system)
    {
        foreach (var pid in self)
        {
            pid.SendSystemMessage(system, message);
        }
    }
    
    public static async Task StopMany(this IEnumerable<PID> self, IContext context)
    {
        foreach (var chunk in self.Chunk(20))
        {
            var tasks = chunk.Select(context.StopAsync);
            await Task.WhenAll(tasks).WaitUpTo(TimeSpan.FromSeconds(10));
        }
    }

    [UsedImplicitly]
    public static void Deconstruct<TKey, TValue>(
        //DON'T TOUCH THIS, it tries to deconstruct the deconstruct method...
        // ReSharper disable once UseDeconstructionOnParameter
        this KeyValuePair<TKey, TValue> self,
        out TKey key,
        out TValue value
    )
    {
        key = self.Key;
        value = self.Value;
    }

    public static void CheckFailFast(this Exception? reason)
    {
        if (reason is not OutOfMemoryException)
        {
            return;
        }

        Console.WriteLine("[Fatal] Out of memory exception" + reason);
        Environment.FailFast(reason.Message, reason);
    }

    public static Process Configure(this Process self)
    {
        var system = self.System;
        return system.Config.ConfigureProcess(self);
    }

    public static Func<T, T> Wrap<T>(this Func<T, T> self, Func<T, T> outer) => 
        x => outer(self(x));
}