// -----------------------------------------------------------------------
//   <copyright file="MessageEnvelope.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Proto
{
    [PublicAPI]
    public class MessageEnvelope
    {
        public MessageEnvelope(object message, PID? sender, MessageHeader? header = null)
        {
            Sender = sender;
            Message = message;
            Header = header ?? MessageHeader.Empty;
        }

        public PID? Sender { get; }
        public object Message { get; }
        public MessageHeader Header { get; }

        public static MessageEnvelope Wrap(object message) =>
            message is MessageEnvelope env ? env : new MessageEnvelope(message, null);

        public MessageEnvelope WithSender(PID sender) => new MessageEnvelope(Message, sender, Header);

        public MessageEnvelope WithMessage(object message) => new MessageEnvelope(message, Sender, Header);

        public MessageEnvelope WithHeader(MessageHeader header) => new MessageEnvelope(Message, Sender, header);

        public MessageEnvelope WithHeader(string key, string value)
        {
            var header = Header.With(key, value);
            return new MessageEnvelope(Message, Sender, header);
        }

        public MessageEnvelope WithHeaders(IEnumerable<KeyValuePair<string, string>> items)
        {
            var header = Header.With(items);
            return new MessageEnvelope(Message, Sender, header);
        }

        public static (object message, PID? sender, MessageHeader headers) Unwrap(object message)
            => message is MessageEnvelope envelope
                ? (envelope.Message, envelope.Sender, envelope.Header)
                : (message, null, MessageHeader.Empty);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MessageHeader UnwrapHeader(object? message) =>
            (message as MessageEnvelope)?.Header ?? MessageHeader.Empty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? UnwrapMessage(object? message) => message is MessageEnvelope r ? r.Message : message;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PID? UnwrapSender(object? message) => (message as MessageEnvelope)?.Sender;
    }
}