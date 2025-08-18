// -----------------------------------------------------------------------
// <copyright file="UnboundedMailbox.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Mailbox;

/// <summary>
/// Factory helpers for creating unbounded mailboxes.
/// </summary>
public static class UnboundedMailbox
{
    public static IMailbox Create(params IMailboxStatistics[] stats) =>
        new DefaultMailbox(new LockingUnboundedMailboxQueue(4), new UnboundedMailboxQueue(), stats);
}
