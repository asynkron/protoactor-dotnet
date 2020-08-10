// -----------------------------------------------------------------------
//   <copyright file="MemberStatus.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Proto.Cluster
{
    [PublicAPI]
    public class MemberStatus
    {
        public MemberStatus(string memberId, string host, int port, IReadOnlyCollection<string> kinds, bool alive)
        {
            MemberId = memberId;
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Kinds = kinds ?? throw new ArgumentNullException(nameof(kinds));
            Port = port;
            Alive = alive;
        }

        public string Address => Host + ":" + Port;
        public string MemberId { get; }
        public string Host { get; }
        public int Port { get; }
        public IReadOnlyCollection<string> Kinds { get; }
        public bool Alive { get; }
    }

}