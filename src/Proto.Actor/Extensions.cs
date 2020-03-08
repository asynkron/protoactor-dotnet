// -----------------------------------------------------------------------
//   <copyright file="Extensions.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Proto.Mailbox;

namespace Proto
{
    public static class Extensions
    {
        public static void Stop(this IEnumerable<PID> self)
        {
            foreach (var pid in self)
            {
                RootContext.Empty.Stop(pid);
            }
        }

        public static void SendSystemMessage(this IEnumerable<PID> self, SystemMessage message)
        {
            foreach (var pid in self)
            {
                pid.SendSystemMessage(message);
            }
        }

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> self, out TKey key,
            out TValue value)
        {
            key = self.Key;
            value = self.Value;
        }
    }
}