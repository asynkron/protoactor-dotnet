using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto
{
    public class ReceiveActor : IActor
    {
        private readonly Dictionary<Type, Func<IContext,Task>> _handlers
            = new Dictionary<Type, Func<IContext, Task>>();

        protected void RegisterHandler<T>(Func<T, IContext, Task> handler)
        {
            _handlers.Add(typeof(T), context => handler.Invoke((T) context.Message, context));
        }

        public Task ReceiveAsync(IContext context)
        {
            var messageType = context.Message.GetType();

            return _handlers.ContainsKey(messageType) 
                ? _handlers[messageType](context) 
                : OnUnknownMessage(context);
        }

        protected virtual Task OnUnknownMessage(IContext context)
        {
            return Actor.Done;
        }
    }
}
