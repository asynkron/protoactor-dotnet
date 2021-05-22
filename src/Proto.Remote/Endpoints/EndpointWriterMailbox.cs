// -----------------------------------------------------------------------
//   <copyright file="EndpointWriterMailbox.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Mailbox;

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
        private readonly string _address;

        private readonly int _batchSize;
        private readonly ActorSystem _system;
        private readonly IMailboxQueue _systemMessages = new UnboundedMailboxQueue();
        private readonly IMailboxQueue _userMessages = new UnboundedMailboxQueue();
        private IDispatcher? _dispatcher;
        private IMessageInvoker? _invoker;

        private int _status = MailboxStatus.Idle;
        private bool _suspended;

        public EndpointWriterMailbox(ActorSystem system, int batchSize, string address)
        {
            _system = system;
            _batchSize = batchSize;
            _address = address;
        }

        public int UserMessageCount => _userMessages.Length;

        public void PostUserMessage(object msg)
        {
            _userMessages.Push(msg);

            // Logger.LogDebug("[EndpointWriterMailbox] received User Message {@Message}", msg);
            Schedule();
        }

        public void PostSystemMessage(object msg)
        {
            _systemMessages.Push(msg);
            

            // Logger.LogDebug("[EndpointWriterMailbox] received System Message {@Message}", msg);
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
            object? m = null;

            try
            {
                // Logger.LogDebug(
                //     "[EndpointWriterMailbox] Running Mailbox Loop HasSystemMessages: {HasSystemMessages} HasUserMessages: {HasUserMessages} Suspended: {Suspended}",
                //     _systemMessages.HasMessages, _userMessages.HasMessages, _suspended
                // );
                var _ = _dispatcher!.Throughput; //not used for batch mailbox
                var batch = new List<RemoteDeliver>(_batchSize);
                var sys = _systemMessages.Pop();

                if (sys is not null)
                {
                    Logger.LogDebug("[EndpointWriterMailbox] Processing System Message {@Message}", sys);

                    _suspended = sys switch
                    {
                        SuspendMailbox _         => true,
                        EndpointConnectedEvent _ => false,
                        _                        => _suspended
                    };

                    m = sys;

                    switch (m)
                    {
                        case EndpointErrorEvent e:
                            if (!_suspended) // Since it's already stopped, there is no need to throw the error
                                await _invoker!.InvokeUserMessageAsync(sys);
                            break;
                        case EndpointConnectedEvent:
                            //endpoint connected event is not a real system message, do not pass it to the invoker
                            break;
                        default:
                            await _invoker!.InvokeSystemMessageAsync(sys);
                            break;
                    }

                    if (sys is Stop)
                    {
                        // Logger.LogWarning("Endpoint writer is stopping...");
                        //Dump messages from user messages queue to deadletter and inform watchers about termination
                        object? usrMsg;
                        var droppedRemoteDeliverCount = 0;
                        var remoteTerminateCount = 0;

                        while ((usrMsg = _userMessages.Pop()) is not null)
                        {
                            switch (usrMsg)
                            {
                                case RemoteWatch msg:
                                    remoteTerminateCount++;
                                    msg.Watcher.SendSystemMessage(_system, new Terminated
                                        {
                                            Why = TerminatedReason.AddressTerminated,
                                            Who = msg.Watchee
                                        }
                                    );
                                    break;
                                case RemoteDeliver rd:
                                    droppedRemoteDeliverCount++;
                                    if (rd.Sender != null)
                                        _system.Root.Send(rd.Sender, new DeadLetterResponse {Target = rd.Target});
                                    _system.EventStream.Publish(new DeadLetterEvent(rd.Target, rd.Message, rd.Sender));
                                    break;
                            }
                        }

                        if (droppedRemoteDeliverCount > 0)
                        {
                            Logger.LogInformation("[EndpointWriterMailbox] Dropped {count} user Messages for {Address}", droppedRemoteDeliverCount,
                                _address
                            );
                        }

                        if (remoteTerminateCount > 0)
                        {
                            Logger.LogInformation("[EndpointWriterMailbox] Sent {Count} remote terminations for {Address}", remoteTerminateCount,
                                _address
                            );
                        }
                    }
                }

                if (!_suspended)
                {
                    batch.Clear();
                    object? msg;

                    while ((msg = _userMessages.Pop()) is not null)
                    {
                        // Logger.LogDebug("[EndpointWriterMailbox] Processing User Message {@Message}", msg);

                        switch (msg)
                        {
                            case RemoteWatch _:
                            case RemoteUnwatch _:
                            case RemoteTerminate _:
                                await _invoker!.InvokeUserMessageAsync(msg);
                                continue;
                        }

                        batch.Add((RemoteDeliver) msg);

                        if (batch.Count >= _batchSize) break;
                    }

                    if (batch.Count > 0)
                    {
                        m = batch;
                        // Logger.LogDebug("[EndpointWriterMailbox] Calling message invoker");
                        await _invoker!.InvokeUserMessageAsync(batch);
                    }
                }
            }
            catch (Exception x)
            {
                // if (x is RpcException rpc && rpc.Status.StatusCode == StatusCode.Unavailable)
                // {
                //     Logger.LogError( "Endpoint writer failed, status unavailable");
                // }
                // else
                // {
                //     Logger.LogError(x, "Endpoint writer failed");
                // }

                _suspended = true;
                _invoker!.EscalateFailure(x, m);
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