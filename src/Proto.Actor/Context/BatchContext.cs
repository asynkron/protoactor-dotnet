// -----------------------------------------------------------------------
// <copyright file="BatchContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Future;

namespace Proto.Context
{
    public class BatchContext: ISenderContext, IDisposable
    {
        private static readonly ILogger Logger = Log.CreateLogger<BatchContext>();

        private readonly ISenderContext _context;
        private readonly CancellationToken _ct;
        private readonly FutureBatchProcess _batchProcess;
        private readonly IEnumerator<IFuture> _futures;

        public BatchContext(ISenderContext contextContext, int batchSize, CancellationToken ct)
        {
            _context = contextContext;
            _ct = ct;
            _batchProcess = new FutureBatchProcess(contextContext.System, batchSize, ct);
            _futures = _batchProcess.Futures.GetEnumerator();
        }

        public async Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
        {
            using var future = GetFuture();
            var messageEnvelope = new MessageEnvelope(message, future.Pid);
            cancellationToken.ThrowIfCancellationRequested();
            _context.Send(target, messageEnvelope);
            var task = future.Task;
            // var task = cancellationToken == default || cancellationToken == _ct ? future.Task : future.Task;
            var result = await task;

            switch (result)
            {
                case DeadLetterResponse:
                    throw new DeadLetterException(target);
                case null:
                case T:
                    return (T) result!;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected message. Was type {result.GetType()} but expected {typeof(T)}"
                    );
            }
        }

        private IFuture GetFuture()
        {
            if (_futures.MoveNext())
            {
                return _futures.Current!;
            }
            Logger.LogWarning("Batch request got more calls than provisioned");
            return new FutureProcess(System);
        }

        
        public T? Get<T>() => _context.Get<T>();

        public void Set<T, TI>(TI obj) where TI : T => _context.Set<T, TI>(obj);

        public void Remove<T>() => _context.Remove<T>();

        public ActorSystem System => _context.System;
        public PID? Parent => _context.Parent;
        public PID Self => _context.Self;
        public PID? Sender => _context.Sender;
        public IActor Actor => _context.Actor;
        public MessageHeader Headers => _context.Headers;
        public object? Message => _context.Message;

        public void Send(PID target, object message) => _context.Send(target, message);

        public void Request(PID target, object message, PID? sender) => _context.Request(target, message, sender);

        public void Dispose()
        {
            _futures.Dispose();
            _batchProcess.Dispose();
        }
    }
}