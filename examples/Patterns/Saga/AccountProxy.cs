// -----------------------------------------------------------------------
// <copyright file="AccountProxy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Proto;
using Saga.Messages;

namespace Saga
{
    class AccountProxy : IActor
    {
        private readonly Func<PID, object> _createMessage;
        private readonly PID _target;

        public AccountProxy(PID target, Func<PID, object> createMessage)
        {
            _target = target;
            _createMessage = createMessage;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    // imagine this is some sort of remote call rather than a local actor call
                    context.Send(_target, _createMessage(context.Self));
                    context.SetReceiveTimeout(TimeSpan.FromMilliseconds(100));
                    break;
                case OK msg:
                    context.CancelReceiveTimeout();
                    context.Send(context.Parent!, msg);
                    break;
                case Refused msg:
                    context.CancelReceiveTimeout();
                    context.Send(context.Parent!, msg);
                    break;
                // This emulates a failed remote call
                case InsufficientFunds:
                case InternalServerError:
                case ReceiveTimeout:
                case ServiceUnavailable: //TODO - this gives us more information than a failure
                    throw new Exception();
            }

            return Task.CompletedTask;
        }
    }
}