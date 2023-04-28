// -----------------------------------------------------------------------
// <copyright file="MemberStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Linq;

namespace Proto.Cluster;

/// <summary>
///     An abstraction for deciding on which member to spawn the virtual actor.
///     See <a href="https://proto.actor/docs/cluster/member-strategies/">Member strategies documentation</a> for more
///     information.
/// </summary>
public interface IMemberStrategy
{
    /// <summary>
    ///     Returns a list of all known members.
    /// </summary>
    /// <returns></returns>
    ImmutableList<Member> GetAllMembers();

    /// <summary>
    ///     Adds a member to the list of known members
    /// </summary>
    /// <param name="member"></param>
    void AddMember(Member member);

    /// <summary>
    ///     Removes a member from the list of known members.
    /// </summary>
    /// <param name="member"></param>
    void RemoveMember(Member member);

    /// <summary>
    ///     Assigns a virtual actor to a member.
    /// </summary>
    /// <param name="senderAddress">Network address of the process that initiated the activation</param>
    /// <returns>Member to spawn on</returns>
    Member? GetActivator(string senderAddress);
}

internal class SimpleMemberStrategy : IMemberStrategy
{
    private readonly RoundRobinMemberSelector _selector;
    private ImmutableList<Member> _members = ImmutableList<Member>.Empty;

    public SimpleMemberStrategy()
    {
        _selector = new RoundRobinMemberSelector(this);
    }

    public ImmutableList<Member> GetAllMembers() => _members;

    //TODO: account for Member.MemberId
    public void AddMember(Member member)
    {
        // Avoid adding the same member twice
        if (_members.Any(x => x.Address == member.Address))
        {
            return;
        }

        _members = _members.Add(member);
    }

    //TODO: account for Member.MemberId
    public void RemoveMember(Member member) => _members = _members.RemoveAll(x => x.Address == member.Address);

    public Member? GetActivator(string senderAddress) => _selector.GetMember();
}