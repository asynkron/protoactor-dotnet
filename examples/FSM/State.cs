using System;
using System.Collections.Generic;
using System.Linq;

namespace FSMExample
{
    public class State<TD> : IEquatable<State<TD>>
    {
        public State(string stateName, TD stateData, TimeSpan? timeout = null, Reason stopReason = null, List<object> replies = null)
        {
            Replies = replies ?? new List<object>();
            StopReason = stopReason;
            Timeout = timeout;
            StateData = stateData;
            StateName = stateName;
        }

        public string StateName { get; }

        public TD StateData { get; }

        public TimeSpan? Timeout { get; }

        public Reason StopReason { get; }

        public List<object> Replies { get; protected set; }

        public State<TD> Copy(TimeSpan? timeout, Reason stopReason = null, List<object> replies = null)
        {
            return new State<TD>(StateName, StateData, timeout, stopReason ?? StopReason, replies ?? Replies);
        }

        public State<TD> ForMax(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.MaxValue) return Copy(timeout);
            return Copy(timeout: null);
        }

        public State<TD> Replying(object replyValue)
        {
            if (Replies == null) Replies = new List<object>();
            var newReplies = Replies.ToArray().ToList();
            newReplies.Add(replyValue);
            return Copy(Timeout, replies: newReplies);
        }

        public State<TD> Using(TD nextStateData)
        {
            return new State<TD>(StateName, nextStateData, Timeout, StopReason, Replies);
        }

        internal State<TD> WithStopReason(Reason reason)
        {
            return Copy(Timeout, reason);
        }

        public override string ToString()
        {
            return StateName + ", " + StateData;
        }

        public bool Equals(State<TD> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(StateName, other.StateName) && EqualityComparer<TD>.Default.Equals(StateData, other.StateData) && Timeout.Equals(other.Timeout) && Equals(StopReason, other.StopReason) && Equals(Replies, other.Replies);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((State<TD>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StateName.GetHashCode();
                hashCode = (hashCode * 397) ^ EqualityComparer<TD>.Default.GetHashCode(StateData);
                hashCode = (hashCode * 397) ^ Timeout.GetHashCode();
                hashCode = (hashCode * 397) ^ (StopReason?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Replies?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}
