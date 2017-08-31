// -----------------------------------------------------------------------
//   <copyright file="MessageHeader.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto
{
    public class MessageHeader : Dictionary<string, string>
    {
        public static readonly MessageHeader EmptyHeader = new MessageHeader();

        public string GetOrDefault(string key, string @default = null)
        {
            return TryGetValue(key, out string value) ? value : @default;
        }
    }
}