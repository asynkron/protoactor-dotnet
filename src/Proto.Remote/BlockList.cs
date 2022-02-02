// -----------------------------------------------------------------------
// <copyright file="BlockList.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Collections.Immutable;
using Proto.Utils;

namespace Proto.Remote
{
    public class BlockList
    {
        private readonly object _lock = new();

        public ImmutableHashSet<string> BlockedMembers { get; private set; } = ImmutableHashSet<string>.Empty;
        public void Block(string memberId)
        {
            lock (_lock)
            {
                BlockedMembers = BlockedMembers.Add(memberId);
            }
        }

        public void Block(IEnumerable<string> memberIds)
        {
            lock (_lock)
            {
                BlockedMembers = BlockedMembers.Union(memberIds);
            }
        }

        public bool IsBlocked(string memberId) => BlockedMembers.Contains(memberId);
    }
}