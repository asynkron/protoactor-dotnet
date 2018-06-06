// -----------------------------------------------------------------------
//   <copyright file="MessageEnvelope.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace Proto
{
    //TODO: make immutable as the same envelope can be sent to multiple targets
    public class MessageEnvelope
    {
        public MessageEnvelope(object message, PID sender, MessageHeader header)
        {
            Sender = sender; 
            Message = message;
            Header = header;
        }

        public PID Sender { get; }
        public object Message { get; }
        public MessageHeader Header { get; }

        public MessageEnvelope WithSender(PID sender)
        {
            return new MessageEnvelope(Message, sender, Header);
        }
        
        public MessageEnvelope WithMessage(object message)
        {
            return new MessageEnvelope(message, Sender, Header);
        }
        
        public MessageEnvelope WithHeader(MessageHeader header)
        {
            return new MessageEnvelope(Message, Sender, header);
        }

        public MessageEnvelope WithHeader(string key, string value)
        {
            var header = Header.With(key, value);
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