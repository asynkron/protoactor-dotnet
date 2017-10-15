// -----------------------------------------------------------------------
//   <copyright file="MemberStatus.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.Cluster
{
    public class MemberStatus
    {
        public MemberStatus(string memberId, string host, int port, IReadOnlyCollection<string> kinds, bool alive, IMemberStatusValue statusValue)
        {
            MemberId = memberId;
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Kinds = kinds ?? throw new ArgumentNullException(nameof(kinds));
            Port = port;
            Alive = alive;
            StatusValue = statusValue;
        }

        public string Address => Host + ":" + Port;
        public string MemberId { get; }
        public string Host { get; }
        public int Port { get; }
        public IReadOnlyCollection<string> Kinds { get; }
        public bool Alive { get; }
        public IMemberStatusValue StatusValue { get; }
    }

    public interface IMemberStatusValue
    {
        bool IsSame(IMemberStatusValue val);
    }

    public interface IMemberStatusValueSerializer
    {
        byte[] ToValueBytes(IMemberStatusValue val);
        IMemberStatusValue FromValueBytes(byte[] val);
    }

    public class DefaultMemberStatusValue : IMemberStatusValue
    {
        public static readonly DefaultMemberStatusValue Default = new DefaultMemberStatusValue {Weight = 5};

        public int Weight { get; set; }

        public bool IsSame(IMemberStatusValue val)
        {
            return Weight == (val as DefaultMemberStatusValue)?.Weight;
        }
    }

    public class DefaultMemberStatusValueSerializer : IMemberStatusValueSerializer
    {
        public byte[] ToValueBytes(IMemberStatusValue val)
        {
            var dVal = (DefaultMemberStatusValue) val;
            return Encoding.UTF8.GetBytes(dVal.Weight.ToString());
        }

        public IMemberStatusValue FromValueBytes(byte[] val)
        {
            var weight =  Encoding.UTF8.GetString(val);
            return new DefaultMemberStatusValue
            {
                Weight = int.Parse(weight)
            };
        }
    }
}