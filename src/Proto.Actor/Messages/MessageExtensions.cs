// -----------------------------------------------------------------------
// <copyright file="MessageExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto;

public partial class Terminated
{
    public static Terminated From(PID who, TerminatedReason why) =>
        new()
        {
            Who = who,
            why_ = why
        };
}