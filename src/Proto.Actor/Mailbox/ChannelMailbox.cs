// -----------------------------------------------------------------------
// <copyright file = "ChannelMailbox.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Proto.Mailbox;

public class ChannelMailbox : IMailbox
{
    private readonly Channel<object> _userMessages = Channel.CreateUnbounded<object>();
    private readonly Channel<object> _systemMessages = Channel.CreateUnbounded<object>();
    private IMessageInvoker _invoker = null!;
    private bool _stopped = false;

    public int UserMessageCount => _userMessages.Reader.Count;

    public void PostUserMessage(object msg) => _userMessages.Writer.WriteAsync(msg).GetAwaiter().GetResult();

    public void PostSystemMessage(object msg) => _systemMessages.Writer.WriteAsync(msg).GetAwaiter().GetResult();

    public void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher)
    {
        _invoker = invoker;
    }

    public void Start()
    {
        if (_stopped)
            return;
      
        _ = Task.Run(async () =>
        {
            while (!_stopped)
            {
                if (_systemMessages.Reader.TryRead(out var sys))
                {
                    _stopped = sys switch
                    {
                        Stop => true,
                        _    => _stopped
                    };

                    var t = _invoker.InvokeSystemMessageAsync(sys);

                    if (!t.IsCompletedSuccessfully)
                    {
                        _ = Await(t.AsTask(), sys);
                        return;
                    }
                    continue;
                }

                if (_userMessages.Reader.TryRead(out var user))
                {
                    var t = _invoker.InvokeUserMessageAsync(user);
                    if (!t.IsCompletedSuccessfully)
                    {
                        _ = Await(t.AsTask(), user);
                        return;
                    }
                    continue;
                }

                var t1 = _systemMessages.Reader.WaitToReadAsync().AsTask();
                var t2 = _userMessages.Reader.WaitToReadAsync().AsTask();
                
                await Task.WhenAny(t1, t2);
            }
        });
    }

    private async Task Await(Task t, object msg)
    {
        try
        {
            await t;
            //reschedule
        }
        catch(Exception x)
        {
            _invoker.EscalateFailure(x, msg);
        }
        Start();
    }
}