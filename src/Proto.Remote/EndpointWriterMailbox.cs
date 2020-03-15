// -----------------------------------------------------------------------
//   <copyright file="EndpointWriterMailbox.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using Proto.Mailbox;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    static class MailboxStatus
    {
        public const int Idle = 0;
        public const int Busy = 1;
    }

    public class EndpointWriterMailbox : IMailbox
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointWriterMailbox>();

        private readonly int _batchSize;
        private readonly IMailboxQueue _systemMessages = new UnboundedMailboxQueue();
        private readonly IMailboxQueue _userMessages = new UnboundedMailboxQueue();
        private readonly ActorSystem _system;
        private IDispatcher _dispatcher;
        private IMessageInvoker _invoker;

        private int _status = MailboxStatus.Idle;
        private bool _suspended;

        public EndpointWriterMailbox(ActorSystem system, int batchSize)
        {
            _system = system;
            _batchSize = batchSize;
        }

        public void PostUserMessage(object msg)
        {
            _userMessages.Push(msg);

            Logger.LogDebug("[EndpointWriterMailbox] received User Message {@Message}", msg);
            Schedule();
        }

        public void PostSystemMessage(object msg)
        {
            _systemMessages.Push(msg);

            Logger.LogDebug("[EndpointWriterMailbox] received System Message {@Message}", msg);
            Schedule();
        }

        public void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher)
        {
            _invoker = invoker;
            _dispatcher = dispatcher;
        }

        public void Start() { }

        private async Task RunAsync()
        {
            object m = null;

            try
            {
                Logger.LogDebug(
                    "[EndpointWriterMailbox] Running Mailbox Loop HasSystemMessages: {HasSystemMessages} HasUserMessages: {HasUserMessages} Suspended: {Suspended}",
                    _systemMessages.HasMessages, _userMessages.HasMessages, _suspended
                );
                var _ = _dispatcher.Throughput; //not used for batch mailbox
                var batch = new List<RemoteDeliver>(_batchSize);
                var sys = _systemMessages.Pop();

                if (sys != null)
                {
                    Logger.LogDebug("[EndpointWriterMailbox] Processing System Message {@Message}", sys);

                    _suspended = sys switch
                    {
                        SuspendMailbox _ => true,
                        EndpointConnectedEvent _ => false,
                        _ => _suspended
                    };

                    m = sys;
                    await _invoker.InvokeSystemMessageAsync(sys);

                    if (sys is Stop)
                    {
                        //Dump messages from user messages queue to deadletter 
                        object usrMsg;

                        while ((usrMsg = _userMessages.Pop()) != null)
                        {
                            if (usrMsg is RemoteDeliver rd)
                            {
                                _system.EventStream.Publish(new DeadLetterEvent(rd.Target, rd.Message, rd.Sender));
                            }
                        }
                    }
                }

                if (!_suspended)
                {
                    batch.Clear();
                    object msg;

                    while ((msg = _userMessages.Pop()) != null)
                    {
                        Logger.LogDebug("[EndpointWriterMailbox] Processing User Message {@Message}", msg);

                        if (msg is EndpointTerminatedEvent) //The mailbox was crashing when it received this particular message 
                        {
                            await _invoker.InvokeUserMessageAsync(msg);
                            continue;
                        }

                        batch.Add((RemoteDeliver)msg);

                        if (batch.Count >= _batchSize)
                        {
                            break;
                        }
                    }

                    if (batch.Count > 0)
                    {
                        m = batch;
                        Logger.LogDebug("[EndpointWriterMailbox] Calling message invoker");
                        await _invoker.InvokeUserMessageAsync(batch);
                    }
                }
            }
            catch (Exception x)
            {
                Logger.LogWarning(x, "[EndpointWriterMailbox] Exception in RunAsync");
                _suspended = true;
                _invoker.EscalateFailure(x, m);
            }

            Interlocked.Exchange(ref _status, MailboxStatus.Idle);

            if (_systemMessages.HasMessages || _userMessages.HasMessages & !_suspended)
            {
                Schedule();
            }
        }

        private void Schedule()
        {
            if (Interlocked.CompareExchange(ref _status, MailboxStatus.Busy, MailboxStatus.Idle) == MailboxStatus.Idle)
            {
                _dispatcher.Schedule(RunAsync);
            }
        }
    }
}