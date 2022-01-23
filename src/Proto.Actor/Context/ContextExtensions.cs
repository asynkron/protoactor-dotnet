// -----------------------------------------------------------------------
// <copyright file="ContextExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Context
{
    public static class ContextExtensions
    {
        /// <summary>
        /// Calls the callback on token cancellation. If CancellationToken is non-cancellable, this is a noop.
        /// </summary>
        public static CancellationTokenRegistration ReenterAfterCancellation(this IContext context, CancellationToken token, Action onCancelled)
        {
            if (token.IsCancellationRequested)
            {
                context.ReenterAfter(Task.CompletedTask, onCancelled);
                return default;
            }

            if (!token.CanBeCanceled) return default;

            var tcs = new TaskCompletionSource<bool>();
            context.ReenterAfter(tcs.Task, onCancelled);
            return token.Register(() => tcs.SetResult(true));
        }
    }
}