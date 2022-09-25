// -----------------------------------------------------------------------
// <copyright file="SupervisorDirective.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace

namespace Proto;

/// <summary>
///     Tells the supervisor what to do in case of failure.
/// </summary>
public enum SupervisorDirective
{
    /// <summary>
    ///     Subject continues processing after failure, starting with the next message in the mailbox.
    /// </summary>
    Resume,

    /// <summary>
    ///     Restarts the subject
    /// </summary>
    Restart,

    /// <summary>
    ///     Permanently stops the subject
    /// </summary>
    Stop,

    /// <summary>
    ///     Escalates the failure to the parent supervisor
    /// </summary>
    Escalate
}