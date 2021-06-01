// -----------------------------------------------------------------------
// <copyright file="BatchingMailbox.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Mailbox
{
    public record MessageBatch(IList<object> Messages);

    public class BatchingMailbox : IMailbox
    {
        private readonly int _batchSize;
        private readonly IMailboxQueue _systemMessages = new UnboundedMailboxQueue();
        private readonly IMailboxQueue _userMessages = new UnboundedMailboxQueue();
        private IDispatcher? _dispatcher;
        private IMessageInvoker? _invoker;

        private int _status = MailboxStatus.Idle;
        private bool _suspended;

        public BatchingMailbox(int batchSize) => _batchSize = batchSize;

        public int UserMessageCount => _userMessages.Length;

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
            object? currentMessage = null;

            try
            {
                var batch = new List<object>(_batchSize);
                var sys = _systemMessages.Pop();

                if (sys is not null)
                {
                    _suspended = sys switch
                    {
                        //special system message at mailbox level
                        SuspendMailbox _ => true,
                        _                => _suspended
                    };
                    currentMessage = sys;
                    await _invoker!.InvokeSystemMessageAsync(sys);
                }

                if (!_suspended)
                {
                    batch.Clear();
                    object? msg;

                    while ((msg = _userMessages.Pop()) is not null ||
                           batch.Count >= _batchSize)
                    {
                        batch.Add(msg!);
                    }

                    if (batch.Count > 0)
                    {
                        currentMessage = batch;
                        await _invoker!.InvokeUserMessageAsync(new MessageBatch(batch));
                    }
                }
            }
            catch (Exception x)
            {
                _suspended = true;
                _invoker!.EscalateFailure(x, currentMessage);
            }

            Interlocked.Exchange(ref _status, MailboxStatus.Idle);

            if (_systemMessages.HasMessages || _userMessages.HasMessages & !_suspended) Schedule();
        }

        private void Schedule()
        {
            if (Interlocked.CompareExchange(ref _status, MailboxStatus.Busy, MailboxStatus.Idle) == MailboxStatus.Idle)
                _dispatcher!.Schedule(RunAsync);
        }
    }
}