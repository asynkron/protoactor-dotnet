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
    internal static class MailboxStatus
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
        private IDispatcher _dispatcher;
        private IMessageInvoker _invoker;

        private int _status = MailboxStatus.Idle;
        private bool _suspended;

        public EndpointWriterMailbox(int batchSize)
        {
            _batchSize = batchSize;
        }

        public void PostUserMessage(object msg)
        {
            _userMessages.Push(msg);
             
            Logger.LogDebug($"EndpointWriterMailbox received User Message added to queue");
            Schedule();
        }

        public void PostSystemMessage(object msg)
        {
            _systemMessages.Push(msg);
            Schedule();
        }

        public void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher)
        {
            _invoker = invoker;
            _dispatcher = dispatcher;
        }

        public void Start()
        {
        }

        private async Task RunAsync()
        {
            object m = null;
            try
            {
                Logger.LogDebug($"Running Mailbox Loop HasSystemMessages: {_systemMessages.HasMessages} HasUserMessages: {_userMessages.HasMessages} suspended: {_suspended}");
                var _ = _dispatcher.Throughput; //not used for batch mailbox
                var batch = new List<RemoteDeliver>(_batchSize);
                var sys = _systemMessages.Pop();
                if (sys != null)
                {
                    Logger.LogDebug($"Processing System Message");
                    if (sys is SuspendMailbox)
                    {
                        _suspended = true;
                    }
                    if (sys is ResumeMailbox)
                    {
                        //Wait till endpoint is connected before allowing messages to flow
                        //_suspended = false;
                    }
                    if (sys is EndpointConnectedEvent){
                        _suspended = false;
                    }
                    m = sys;
                    await _invoker.InvokeSystemMessageAsync(sys);
                    if (sys is Stop)
                    {
                        //Dump messages from user messages queue to deadletter 
                        object usrMsg;
                        while ((usrMsg = _userMessages.Pop()) != null)
                        {
                            if(usrMsg is RemoteDeliver rd){
                                EventStream.Instance.Publish(new DeadLetterEvent(rd.Target, rd.Message, rd.Sender));
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
                        Logger.LogDebug($"Processing User Message");
                        
                        if (msg is EndpointTerminatedEvent) //The mailbox was crashing when it received this particular message 
                        {
                            await _invoker.InvokeUserMessageAsync(msg);
                            continue;
                        }

                        batch.Add((RemoteDeliver) msg);
                        if (batch.Count >= _batchSize)
                        {
                            break;
                        }
                    }

                    if (batch.Count > 0)
                    {
                        m = batch;
                        Logger.LogDebug($"Calling message invoker");
                        await _invoker.InvokeUserMessageAsync(batch);
                    }
                }

            }
            catch (Exception x)
            {
                Logger.LogWarning("Exception in RunAsync", x);
                _suspended = true;
                _invoker.EscalateFailure(x,m);
                
            }

            Interlocked.Exchange(ref _status, MailboxStatus.Idle);

            if (_systemMessages.HasMessages || ( _userMessages.HasMessages &! _suspended )  )
            {
                Schedule();
            }
        }

        protected void Schedule()
        {
           
            if (Interlocked.CompareExchange(ref _status, MailboxStatus.Busy, MailboxStatus.Idle) == MailboxStatus.Idle)
            {
                _dispatcher.Schedule(RunAsync);
            }
        }
    }
}