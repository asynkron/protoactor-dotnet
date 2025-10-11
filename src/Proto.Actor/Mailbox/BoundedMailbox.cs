// -----------------------------------------------------------------------
// <copyright file="BoundedMailbox.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Channels;

namespace Proto.Mailbox;

/// <summary>
/// Factory helpers for creating bounded mailboxes.
/// </summary>
public static class BoundedMailbox
{
    public static IMailbox Create(int size, params IMailboxStatistics[] stats) =>
        new DefaultMailbox(new LockingUnboundedMailboxQueue(4), new BoundedMailboxQueue(size), stats);

    public static IMailbox Create(int size, BoundedChannelFullMode fullMode, params IMailboxStatistics[] stats) =>
        new DefaultMailbox(new LockingUnboundedMailboxQueue(4), new BoundedMailboxQueue(size, fullMode), stats);
}
