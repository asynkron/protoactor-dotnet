// -----------------------------------------------------------------------
// <copyright file="BlockList.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Proto.Remote;

public record MemberBlocked(string MemberId);
public class BlockList
{
    private readonly ActorSystem _system;

    public BlockList(ActorSystem system)
    {
        _system = system;
    }
    private readonly object _lock = new();

    public ImmutableHashSet<string> BlockedMembers  => _blockedMembers
        .Where(kvp => kvp.Value > DateTime.UtcNow.AddHours(-1))
        .Select(kvp => kvp.Key)
        .ToImmutableHashSet();

    private ImmutableDictionary<string, DateTime> _blockedMembers  = ImmutableDictionary<string,DateTime>.Empty;

    public void Block(IEnumerable<string> memberIds)
    {
        lock (_lock)
        {
            foreach (var member in memberIds)
            {
                _blockedMembers = _blockedMembers.ContainsKey(member) switch
                {
                    false => _blockedMembers.Add(member, DateTime.UtcNow),
                    _     => _blockedMembers.SetItem(member, DateTime.UtcNow)
                };
                _system.EventStream.Publish(new MemberBlocked(member));
            }
        }
    }

    public bool IsBlocked(string memberId) => _blockedMembers.ContainsKey(memberId);
}