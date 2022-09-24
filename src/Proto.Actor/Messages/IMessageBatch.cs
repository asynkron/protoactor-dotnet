// -----------------------------------------------------------------------
// <copyright file="IMessageBatch.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto;

/// <summary>
///     Marks a message as a batch. This batch will be unpacked into the recipient's queue. If additionally the batch
///     message implements
///     <see cref="IAutoRespond" />, the batch message itself will be posted into the recipient's queue immediately after
///     unpacked messages.
/// </summary>
public interface IMessageBatch
{
    // ReSharper disable once ReturnTypeCanBeEnumerable.Global
    /// <summary>
    ///     Unpack the messages from the batch
    /// </summary>
    /// <returns>Collection of messages in the batch</returns>
    IReadOnlyCollection<object> GetMessages();
}