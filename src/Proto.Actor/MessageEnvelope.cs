// -----------------------------------------------------------------------
//   <copyright file="MessageEnvelope.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Proto
{
    //TODO: make immutable as the same envelope can be sent to multiple targets
    public class MessageEnvelope
    {
        public MessageEnvelope()
        {
        }

        public MessageEnvelope(object message, PID sender, MessageHeader header)
        {
            Sender = sender; 
            Message = message;
            Header = header;
        }

        public static MessageEnvelope Wrap(object message)
        {
            if (message is MessageEnvelope env)
            {
                return env;
            }
            return new MessageEnvelope(message, null, null);
        }

        public PID Sender { get; }
        public object Message { get; }
        public MessageHeader Header { get; }

        public MessageEnvelope WithSender(PID sender) => new MessageEnvelope(Message, sender, Header);

        public MessageEnvelope WithMessage(object message) => new MessageEnvelope(message, Sender, Header);

        public MessageEnvelope WithHeader(MessageHeader header) => new MessageEnvelope(Message, Sender, header);

        public MessageEnvelope WithHeader(string key, string value)
        {
            var header = (Header ?? new MessageHeader()).With(key, value);
            return new MessageEnvelope(Message, Sender, header);
        }
        
        public MessageEnvelope WithHeaders(IEnumerable<KeyValuePair<string, string>> items)
        {
            var header = (Header ?? new MessageHeader()).With(items);
            return new MessageEnvelope(Message, Sender, header);

        }

        public static (object message, PID sender, MessageHeader headers) Unwrap(object message)
        {
            if (message is MessageEnvelope envelope)
            {
                return (envelope.Message, envelope.Sender, envelope.Header);
            }

            return (message, null, null);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MessageHeader UnwrapHeader(object message)
        {
            if (message is MessageEnvelope messageEnvelope && messageEnvelope.Header != null)
            {
                return messageEnvelope.Header;
            }
            return MessageHeader.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object UnwrapMessage(object message) => message is MessageEnvelope r ? r.Message : message;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PID UnwrapSender(object message) => (message as MessageEnvelope)?.Sender;
    }
}