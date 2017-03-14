// -----------------------------------------------------------------------
//  <copyright file="EndpointWriterMailbox.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.Remote
{
    internal static class MailboxStatus
    {
        public const int Idle = 0;
        public const int Busy = 1;
    }

    public class EndpointWriterMailbox : IMailbox
    {
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
            var t = _dispatcher.Throughput;
            var batch = new List<RemoteDeliver>(_batchSize);
            var sys = _systemMessages.Pop();
            if (sys != null)
            {
                if (sys is SuspendMailbox)
                {
                    _suspended = true;
                }
                if (sys is ResumeMailbox)
                {
                    _suspended = false;
                }
                await _invoker.InvokeSystemMessageAsync(sys);
            }
            if (!_suspended)
            {
                batch.Clear();
                object msg;
                while ((msg = _userMessages.Pop()) != null)
                {
                    batch.Add((RemoteDeliver) msg);
                    if (batch.Count >= _batchSize)
                    {
                        break;
                    }
                }

                if (batch.Count > 0)
                {
                    await _invoker.InvokeUserMessageAsync(batch);
                }
            }


            Interlocked.Exchange(ref _status, MailboxStatus.Idle);

            if (_userMessages.HasMessages || _systemMessages.HasMessages)
            {
                Schedule();
            }
        }

        protected void Schedule()
        {
            if (Interlocked.Exchange(ref _status, MailboxStatus.Busy) == MailboxStatus.Idle)
            {
                _dispatcher.Schedule(RunAsync);
            }
        }
    }
}