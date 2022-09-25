// -----------------------------------------------------------------------
// <copyright file="CancellationTokens.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Proto;

public static class CancellationTokens
{
    private static readonly ConcurrentDictionary<int, TokenEntry> Tokens = new();

    /// <summary>
    ///     Gets a cancellation token that will be cancelled after the specified time rounded up to nearest second.
    /// </summary>
    /// <remarks>
    ///     The tokens creation is optimized and they are reused, hence cancellation timeout can be off by (at most) 500ms.
    ///     Because of that it is best to use this method when the timeout is at least several seconds
    /// </remarks>
    /// <param name="duration"></param>
    /// <returns></returns>
    public static CancellationToken FromSeconds(TimeSpan duration)
    {
        var seconds = (int)Math.Ceiling(duration.TotalSeconds);

        return FromSeconds(seconds);
    }

    /// <summary>
    ///     Gets a cancellation token that will be cancelled after specified number of seconds.
    /// </summary>
    /// <param name="seconds"></param>
    /// <remarks>
    ///     The tokens creation is optimized and they are reused, hence cancellation timeout can be off by (at most) 500ms.
    ///     Because of that it is best to use this method when the timeout is at least several seconds
    /// </remarks>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if seconds is less than 1</exception>
    public static CancellationToken FromSeconds(int seconds)
    {
        if (seconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        static TokenEntry ValueFactory(int seconds)
        {
            var cts = new CancellationTokenSource(seconds * 1000);

            return new TokenEntry(DateTimeOffset.Now, cts.Token);
        }

        while (true)
        {
            var x = Tokens.GetOrAdd(seconds, ValueFactory);

            //is the entry expired?
            //token reuse expire after 500ms
            if (x.Timestamp >= DateTimeOffset.Now.AddMilliseconds(-500))
            {
                return x.Token;
            }

            Tokens.TryRemove(seconds, out _);
        }
    }

    [Obsolete("Use and dispose CancellationTokenSource, or use CancellationTokens.FromSeconds", true)]
    public static CancellationToken WithTimeout(int ms) => new CancellationTokenSource(ms).Token;

    /// <summary>
    ///     Creates a new CancellationTokenSource that will be cancelled after the specified time.
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    public static CancellationToken WithTimeout(TimeSpan timeSpan) => new CancellationTokenSource(timeSpan).Token;

    private record TokenEntry(DateTimeOffset Timestamp, CancellationToken Token);
}