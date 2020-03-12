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
        public static void Stop(this IEnumerable<PID> self, ActorSystem system)
        {
            if (self == null)
            {
                return;
            }
            
            foreach (var pid in self)
            {
                system.Root.Stop(pid);
            }
        }
        
        public static void SendSystemMessage( this IEnumerable<PID> self, SystemMessage message, ActorSystem system)
        {
            foreach (var pid in self)
            {
                pid.SendSystemMessage(system, message);
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