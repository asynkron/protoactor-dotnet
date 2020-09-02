// -----------------------------------------------------------------------
//   <copyright file="RemoteKindRegistry.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Remote
{
    public class RemoteKindRegistry
    {
        private readonly Dictionary<string, Props> _kinds = new Dictionary<string, Props>();
        public string[] GetKnownKinds() => _kinds.Keys.ToArray();
        public void RegisterKnownKind(string kind, Props props) => _kinds.Add(kind, props);
        public void UnregisterKnownKind(string kind) => _kinds.Remove(kind);
        public Props GetKnownKind(string kind)
        {
            if (_kinds.TryGetValue(kind, out var props))
            {
                return props;
            }

            throw new ArgumentException($"No Props found for kind '{kind}'");
        }
    }
}