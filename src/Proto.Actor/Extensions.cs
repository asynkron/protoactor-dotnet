// -----------------------------------------------------------------------
//   <copyright file="Extensions.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto
{
    public static class Extensions
    {
        public static void Send(this PID self, object message)
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