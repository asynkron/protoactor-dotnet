// -----------------------------------------------------------------------
//   <copyright file="Extensions.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
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
                pid.Stop();
            }
        }
        
        public static void SendSystemNessage(this IEnumerable<PID> self, SystemMessage message)
        {
            foreach (var pid in self)
            {
                pid.SendSystemMessage(message);
            }
        }
        
        
        [Obsolete("Replaced with Context.Send(msg)", false)]
        public static void Tell(this PID self, object message)
        {
            self.SendUserMessage(message);
        }
        
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> self, out TKey key, out TValue value)
        {
            key = self.Key;
            value = self.Value;
        }
    }
}