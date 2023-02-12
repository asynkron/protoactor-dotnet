using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Context;

[PublicAPI]
public static class PassivationContextExtensions
{
    public static Props WithPassivationContextDecorator(this Props props, TimeSpan timeout) => 
        props.WithContextDecorator(ctx => new PassivationContextDecorator(ctx, timeout));
}
public class PassivationContextDecorator : ActorContextDecorator
{
    private readonly IContext _context;
    private readonly TimeSpan _timeout;

    public PassivationContextDecorator(IContext context, TimeSpan timeout) : base(context)
    {
        _context = context;
        _timeout = timeout;
    }

    public override Task Receive(MessageEnvelope envelope)
    {
        var msg = envelope.Message;

        if (msg is Started)
        {
            _context.SetReceiveTimeout(_timeout);
        }
        else if (msg is ReceiveTimeout)
        {
            _context.Stop(_context.Self);
            return Task.CompletedTask;
        }

        return base.Receive(envelope);
    }
}
