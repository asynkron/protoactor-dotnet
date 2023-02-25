// -----------------------------------------------------------------------
// <copyright file="DeadlineContextDecorator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Context;

[PublicAPI]
public static class DeadlineContextExtensions
{
    /// <summary>
    ///     Adds a decorator for a <see cref="ActorContext" /> that logs warning message if Receive takes more time than
    ///     specified timeout.
    /// </summary>
    /// <param name="props"></param>
    /// <param name="deadline">The timeout for Receive to complete</param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static Props WithDeadlineDecorator(
        this Props props,
        TimeSpan deadline,
        ILogger logger
    ) =>
        props.WithContextDecorator(ctx => new DeadlineContextDecorator(ctx, deadline, logger));
}

/// <summary>
///     A decorator for a <see cref="ActorContext" /> that logs warning message if Receive takes more time than specified
///     timeout.
/// </summary>
public class DeadlineContextDecorator : ActorContextDecorator
{
    private readonly IContext _context;
    private readonly TimeSpan _deadline;
    private readonly ILogger _logger;

    public DeadlineContextDecorator(IContext context, TimeSpan deadline, ILogger logger) : base(context)
    {
        _deadline = deadline;
        _logger = logger;
        _context = context;
    }

    public override async Task Receive(MessageEnvelope envelope)
    {
        var t = base.Receive(envelope);

        if (t.IsCompleted)
        {
            return;
        }

        var ok = await t.WaitUpTo(_deadline).ConfigureAwait(false);

        if (!ok)
        {
            _logger.LogWarning("Actor {Self} deadline {Deadline}, exceeded on message {Message}", _context.Self,
                _deadline, envelope.Message);

            // keep waiting, we cannot just ignore and continue as an async task might still be running and updating state of the actor
            // if we return here, actor concurrency guarantees could break
            await t.ConfigureAwait(false);
        }
    }
}