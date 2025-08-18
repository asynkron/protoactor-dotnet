// -----------------------------------------------------------------------
// <copyright file="IMailbox.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Mailbox;

/// <summary>
/// Defines the core operations of a mailbox used by actors.
/// </summary>
public interface IMailbox
{
    int UserMessageCount { get; }

    void PostUserMessage(object msg);

    void PostSystemMessage(object msg);

    void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher);

    void Start();
}
