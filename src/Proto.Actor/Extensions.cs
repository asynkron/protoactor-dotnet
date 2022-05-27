﻿// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Proto.Mailbox;

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

    [UsedImplicitly]
    public static void Deconstruct<TKey, TValue>(
        //DONT TOUCH THIS, it tries to deconstruct the deconstruct method...
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
        if (reason is null) return;
        if (reason is not OutOfMemoryException) return;

        Console.WriteLine("[Fatal] Out of memory exception" + reason.ToString());
        Environment.FailFast(reason.Message, reason);
    }
}