// -----------------------------------------------------------------------
// <copyright file="BlockList.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Utils;

namespace Proto.Remote
{
    public class BlockList
    {
        private readonly ConcurrentSet<string> _blockedMembers = new();

        public void Block(string memberId) => _blockedMembers.Add(memberId);

        public bool IsBlocked(string memberId) => _blockedMembers.Contains(memberId);
    }
}