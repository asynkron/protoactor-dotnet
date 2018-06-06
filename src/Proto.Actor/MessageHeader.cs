// -----------------------------------------------------------------------
//   <copyright file="MessageHeader.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto
{
    public class MessageHeader : Dictionary<string, string>
    {
        public static MessageHeader Empty => new MessageHeader();

        public string GetOrDefault(string key, string @default = null)
        {
            return TryGetValue(key, out var value) ? value : @default;
        }
    }
}