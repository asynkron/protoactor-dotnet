using System;
using System.Collections.Generic;
using System.Text;

namespace Proto
{
    public static class Extensions
    {
        public static void Deconstruct<TKey,TValue>(this KeyValuePair<TKey, TValue> self,out TKey key, out TValue value)
        {
            key = self.Key;
            value = self.Value;
        }
    }
}
