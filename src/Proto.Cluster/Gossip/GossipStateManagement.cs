// -----------------------------------------------------------------------
// <copyright file="GossipState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster
{
    public static class GossipStateManagement
    {
        public static GossipKeyValue EnsureEntryExists(GossipMemberState memberState, string key)
        {
            if (!memberState.Values.TryGetValue(key,out var value))
            {
                value = new GossipKeyValue();
                memberState.Values.Add(key, value);
            }

            return value;
        }
        
        public static GossipMemberState EnsureMemberStateExists(GossipState state, string memberId)
        {
            if (!state.Members.TryGetValue(memberId, out var memberState))
            {
                memberState = new GossipMemberState();
                state.Members.Add(memberId, memberState);
            }

            return memberState;
        }

        public static (bool dirty, GossipState newState) MergeState(GossipState state, GossipState remoteState)
        {
            var newState = state.Clone();
            bool dirty = false;

            foreach (var (memberId, remoteMemberState) in remoteState.Members)
            {
                //this entry does not exist in newState, just copy all of it
                if (!newState.Members.ContainsKey(memberId))
                {
                    newState.Members.Add(memberId, remoteMemberState);
                    dirty = true;
                    continue;
                }

                //this entry exists in both newState and remoteState, we should merge them
                var newMemberState = newState.Members[memberId];

                foreach (var (key, remoteValue) in remoteMemberState.Values)
                {
                    //this entry does not exist in newMemberState, just copy all of it
                    if (!newMemberState.Values.ContainsKey(key))
                    {
                        newMemberState.Values.Add(key, remoteValue);
                        dirty = true;
                        continue;
                    }

                    var newValue = newMemberState.Values[key];

                    if (remoteValue.SequenceNumber > newValue.SequenceNumber)
                    {
                        dirty = true;
                        //just replace the existing value
                        newMemberState.Values[key] = remoteValue;
                    }
                }
            }

            return (dirty, newState);
        }
    }
}