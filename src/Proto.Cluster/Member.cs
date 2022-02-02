// -----------------------------------------------------------------------
// <copyright file="Member.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;

namespace Proto.Cluster
{
    [PublicAPI]
    public partial class Member
    {
        public string Address => Host + ":" + Port;

        public string ToLogString() => $"Member Address:{Address} ID:{Id}";
    }
}