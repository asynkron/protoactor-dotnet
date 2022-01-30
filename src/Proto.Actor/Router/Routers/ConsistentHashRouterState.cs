// -----------------------------------------------------------------------
// <copyright file="ConsistentHashRouterState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Proto.Router.Routers
{
    class ConsistentHashRouterState : RouterState
    {
        private readonly Func<string, uint> _hash;
        private readonly Func<object, string>? _messageHasher;
        private readonly int _replicaCount;
        private readonly ISenderContext _senderContext;
        private HashRing<PID>? _hashRing;

        public ConsistentHashRouterState(
            ISenderContext senderContext,
            Func<string, uint> hash,
            int replicaCount,
            Func<object, string>? messageHasher
        )
        {
            _senderContext = senderContext;
            _hash = hash;
            _replicaCount = replicaCount;
            _messageHasher = messageHasher;
        }

        public override void SetRoutees(PID[] routees)
        {
            base.SetRoutees(routees);
            _hashRing = new HashRing<PID>(routees, pid => pid.ToString(), _hash, _replicaCount);
        }

        public override void RouteMessage(object message)
        {
            if (_hashRing is null) throw new InvalidOperationException("Routees not set");

            var env = MessageEnvelope.Unwrap(message);

            if (env.message is IHashable hashable)
            {
                var key = hashable.HashBy();
                var routee = _hashRing.GetNode(key);

                //by design, just forward message
                _senderContext.Send(routee, message);
            }
            else if (_messageHasher is not null)
            {
                var key = _messageHasher(message);
                var routee = _hashRing.GetNode(key);

                //by design, just forward message
                _senderContext.Send(routee, message);
            }
            else
            {
                throw new NotSupportedException(
                    $"Message of type '{message.GetType().Name}' does not implement IHashable"
                );
            }
        }
    }
}