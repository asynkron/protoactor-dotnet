// -----------------------------------------------------------------------
// <copyright file="RouterMessages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;

namespace Proto.Router.Messages
{
    public record Routees(List<PID> Pids);

    public abstract record RouterManagementMessage;

    public record RouterAddRoutee(PID Pid) : RouterManagementMessage;

    public record RouterBroadcastMessage(object Message) : RouterManagementMessage;

    public record RouterRemoveRoutee(PID Pid) : RouterManagementMessage;

    public record RouterGetRoutees : RouterManagementMessage;
}