// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using JetBrains.Annotations;
using Proto.Mailbox;

namespace Proto
{
    public static class UtilExtensions
    {
        /// <summary>
        ///     Stop the current actor
        /// </summary>
        /// <param name="self">The actor instance to stop</param>
        /// <param name="system">Actor system</param>
        internal static void Stop(this IEnumerable<PID> self, ActorSystem system)
        {
            if (self is null!) return;

            foreach (var pid in self)
            {
                system.Root.Stop(pid);
            }
        }

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
    }
}