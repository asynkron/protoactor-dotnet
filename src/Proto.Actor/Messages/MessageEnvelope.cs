// -----------------------------------------------------------------------
// <copyright file="MessageEnvelope.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Proto.Diagnostics;
using Proto.Extensions;

namespace Proto;

/// <summary>
///     Adds headers and sender information to a message.
/// </summary>
[PublicAPI]
public record MessageEnvelope : IDiagnosticsTypeName
{
    /// <summary>
    ///     Creates a new message envelope.
    /// </summary>
    /// <param name="message">Message to wrap</param>
    /// <param name="sender">Sender <see cref="PID" /></param>
    /// <param name="header">Headers</param>
    public MessageEnvelope(object message, PID? sender, MessageHeader? header = null)
    {
        Sender = sender;
        Message = message;
        Header = header ?? MessageHeader.Empty;
    }

    /// <summary>
    ///     Message sender <see cref="PID" />
    /// </summary>
    public PID? Sender { get; init; }

    /// <summary>
    ///     Wrapped message
    /// </summary>
    public object Message { get; init; }

    /// <summary>
    ///     Message headers
    /// </summary>
    public MessageHeader Header { get; init; }

    /// <summary>
    ///     Creates a new message envelope.
    /// </summary>
    /// <param name="message">Message to wrap</param>
    /// <returns></returns>
    public static MessageEnvelope Wrap(object message) =>
        message is MessageEnvelope env ? env : new MessageEnvelope(message, null);

    /// <summary>
    ///     Creates a new message envelope.
    /// </summary>
    /// <param name="message">Message to wrap</param>
    /// <param name="header">Message headers</param>
    /// <returns></returns>
    public static MessageEnvelope Wrap(object message, MessageHeader header) => message is MessageEnvelope env
        ? env.MergeHeader(header)
        : new MessageEnvelope(message, null, header);

    /// <summary>
    ///     Adds a sender <see cref="PID" /> to the message envelope.
    /// </summary>
    /// <param name="sender"></param>
    /// <returns>New envelope</returns>
    public MessageEnvelope WithSender(PID sender) => this with { Sender = sender };

    /// <summary>
    ///     Adds the wrapped message to the message envelope.
    /// </summary>
    /// <param name="message"></param>
    /// <returns>New envelope</returns>
    public MessageEnvelope WithMessage(object message) => this with { Message = message };

    /// <summary>
    ///     Adds the headers to the message envelope.
    /// </summary>
    /// <param name="header"></param>
    /// <returns>New envelope</returns>
    public MessageEnvelope WithHeader(MessageHeader header) => this with { Header = header };

    /// <summary>
    ///     Extends the message envelope with additional headers.
    /// </summary>
    /// <param name="header"></param>
    /// <returns>New envelope</returns>
    public MessageEnvelope MergeHeader(MessageHeader header)
    {
        if (header.Count == 0)
        {
            return this;
        }

        return this with { Header = Header.With(header) };
    }

    /// <summary>
    ///     Adds a header to the message envelope.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns>New envelope</returns>
    public MessageEnvelope WithHeader(string key, string value)
    {
        var header = Header.With(key, value);

        return this with { Header = header };
    }

    /// <summary>
    ///     Extends the message envelope with additional headers.
    /// </summary>
    /// <param name="items"></param>
    /// <returns>New envelope</returns>
    public MessageEnvelope WithHeaders(IEnumerable<KeyValuePair<string, string>> items)
    {
        var header = Header.With(items);

        return this with { Header = header };
    }

    /// <summary>
    ///     Unpacks the message envelope to a tuple. If provided message is not a <see cref="MessageEnvelope" />, it is
    ///     returned as is.
    /// </summary>
    /// <param name="message"><see cref="MessageEnvelope" /> or any other message object</param>
    /// <returns></returns>
    public static (object message, PID? sender, MessageHeader headers) Unwrap(object message) =>
        message is MessageEnvelope envelope
            ? (envelope.Message, envelope.Sender, envelope.Header)
            : (message, null, MessageHeader.Empty);

    /// <summary>
    ///     Returns the message headers if provided message is a <see cref="MessageEnvelope" />.
    /// </summary>
    /// <param name="message"><see cref="MessageEnvelope" /> or any other message object</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MessageHeader UnwrapHeader(object? message) =>
        (message as MessageEnvelope)?.Header ?? MessageHeader.Empty;

    /// <summary>
    ///     Returns the wrapped message if provided message is a <see cref="MessageEnvelope" />.
    /// </summary>
    /// <param name="message"><see cref="MessageEnvelope" /> or any other message object</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? UnwrapMessage(object? message) => message is MessageEnvelope r ? r.Message : message;

    /// <summary>
    ///     Returns the message sender if provided message is a <see cref="MessageEnvelope" />.
    /// </summary>
    /// <param name="message"><see cref="MessageEnvelope" /> or any other message object</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PID? UnwrapSender(object? message) => (message as MessageEnvelope)?.Sender;

    public string GetTypeName()
    {
        var m = Message.GetMessageTypeName();
        return m;
    }
}