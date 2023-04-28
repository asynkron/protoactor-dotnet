// -----------------------------------------------------------------------
// <copyright file="Member.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using JetBrains.Annotations;

namespace Proto.Cluster;

[PublicAPI]
public partial class Member
{
    /// <summary>
    ///     Gets the network address of the member.
    /// </summary>
    public string Address => Host + ":" + Port;

    /// <summary>
    ///     Gets a representation of this member for logging
    /// </summary>
    /// <returns></returns>
    public string ToLogString() => $"Member Address:{Address} ID:{Id}";
}