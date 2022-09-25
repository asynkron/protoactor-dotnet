// -----------------------------------------------------------------------
// <copyright file="PID.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using Google.Protobuf;
using Proto.Mailbox;

namespace Proto;

/// <summary>
///     PID is a reference to an actor (or any other process). It consists of actor system address and an identifier.
/// </summary>
// ReSharper disable once InconsistentNaming
public partial class PID : ICustomDiagnosticMessage
{
    /// <summary>
    ///     Creates a new PID instance from address and identifier.
    /// </summary>
    /// <param name="address">Actor system address</param>
    /// <param name="id">Actor identifier</param>
    public PID(string address, string id)
    {
        Address = address;
        Id = id;
    }

    internal PID(string address, string id, Process process) : this(address, id)
    {
        CurrentRef = process;
    }

    internal Process? CurrentRef { get; private set; }

    public string ToDiagnosticString() => $"{Address}/{Id}";

    /// <summary>
    ///     Creates a new PID instance from address and identifier.
    /// </summary>
    /// <param name="address">Actor system address</param>
    /// <param name="id">Actor identifier</param>
    public static PID FromAddress(string address, string id) => new(address, id);

    internal Process? Ref(ActorSystem system)
    {
        if (CurrentRef is not null)
        {
            if (CurrentRef is ActorProcess { IsDead: true })
            {
                CurrentRef = null;
            }

            return CurrentRef;
        }

        var reff = system.ProcessRegistry.Get(this);

        if (reff is not DeadLetterProcess)
        {
            CurrentRef = reff;
        }

        return CurrentRef;
    }

    internal void SendUserMessage(ActorSystem system, object message)
    {
        var reff = Ref(system) ?? system.ProcessRegistry.Get(this);
        reff.SendUserMessage(this, message);
    }

    internal void SendSystemMessage(ActorSystem system, SystemMessage sys)
    {
        var reff = Ref(system) ?? system.ProcessRegistry.Get(this);
        reff.SendSystemMessage(this, sys);
    }

    /// <summary>
    ///     Stops the referenced actor.
    /// </summary>
    /// <param name="system">Actor system this PID belongs to</param>
    public void Stop(ActorSystem system)
    {
        var reff = CurrentRef ?? system.ProcessRegistry.Get(this);
        reff.Stop(this);
    }

    /// <summary>
    ///     Used internally to track requests in context of shared futures and future batches.
    /// </summary>
    /// <param name="requestId"></param>
    /// <returns></returns>
    public PID WithRequestId(uint requestId) =>
        new()
        {
            Id = Id,
            Address = Address,
            CurrentRef = CurrentRef,
            RequestId = requestId
        };
}