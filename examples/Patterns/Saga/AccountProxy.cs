using System;
using System.Threading.Tasks;
using Proto;
using Saga.Messages;

namespace Saga
{
    class AccountProxy : IActor
    {
        private readonly PID _target;
        private readonly Func<PID, object> _createMessage;

        public AccountProxy(PID target, Func<PID, object> createMessage)
        {
            _target = target;
            _createMessage = createMessage;
        }
        
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    // imagine this is some sort of remote call rather than a local actor call
                    _target.Tell(_createMessage(context.Self));
                    context.SetReceiveTimeout(TimeSpan.FromMilliseconds(100));
                    break;
                case OK msg:
                    context.CancelReceiveTimeout();
                    context.Parent.Tell(msg);
                    break;
                case Refused msg:
                    context.CancelReceiveTimeout();
                    context.Parent.Tell(msg);
                    break;
                // This emulates a failed remote call
                case InsufficientFunds _:
                case InternalServerError _:
                case ReceiveTimeout _:
                case ServiceUnavailable _: //TODO - this gives us more information than a failure
                    throw new Exception();
            }
            
            return Actor.Done;
        }
    }
}