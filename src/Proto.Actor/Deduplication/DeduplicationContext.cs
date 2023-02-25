// -----------------------------------------------------------------------
// <copyright file="DeduplicationContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Deduplication;

/// <summary>
///     Extracts the deduplication key from the message.
/// </summary>
/// <typeparam name="T">Type of the key</typeparam>
/// <param name="envelope">Message to extract from</param>
/// <param name="key">The key should be returned in this variable</param>
/// <returns>Returns true if the key was successfully extracted, false otherwise</returns>
public delegate bool TryGetDeduplicationKey<T>(MessageEnvelope envelope, out T? key);

/// <summary>
///     A decorator for actor context that de-duplicates incoming messages based on the message's deduplication key.
/// </summary>
/// <typeparam name="T">Type of the deduplication key</typeparam>
public class DeduplicationContext<T> : ActorContextDecorator where T : IEquatable<T>
{
    private readonly DeDuplicator<T> _deDuplicator;

    public DeduplicationContext(IContext context, TimeSpan deDuplicationWindow, TryGetDeduplicationKey<T> deduplicateBy)
        : base(context
        )
    {
        _deDuplicator = new DeDuplicator<T>(deDuplicationWindow, deduplicateBy);
    }

    public override Task Receive(MessageEnvelope envelope) =>
        _deDuplicator.DeDuplicate(envelope, () => base.Receive(envelope));
}

/// <summary>
///     Will deduplicate on a sender id if the sender is an unnamed actor (ie a FutureProcess)
/// </summary>
internal class DeDuplicator<T> where T : IEquatable<T>

{
    private readonly TryGetDeduplicationKey<T> _getDeduplicationKey;
    private readonly ILogger _logger = Log.CreateLogger<DeDuplicator<T>>();

    private readonly Dictionary<T, long> _processed = new(50);
    private readonly long _ttl;
    private long _cleanedAt;
    private long _lastCheck;
    private long _oldest;

    public DeDuplicator(TimeSpan deduplicationWindow, TryGetDeduplicationKey<T> getDeduplicationKey)
    {
        _getDeduplicationKey = getDeduplicationKey;
        _ttl = Stopwatch.Frequency * (long)deduplicationWindow.TotalSeconds;
    }

    public async Task DeDuplicate(MessageEnvelope envelope, Func<Task> continuation)
    {
        if (_getDeduplicationKey(envelope, out var key))
        {
            var now = Stopwatch.GetTimestamp();
            var cutoff = now - _ttl;

            if (IsDuplicate(key!, cutoff))
            {
                _logger.LogInformation("Request de-duplicated");

                return;
            }

            await continuation().ConfigureAwait(false);
            CleanIfNeeded(cutoff, now);
            _lastCheck = now;
            Add(key!, now);

            return;
        }

        await continuation().ConfigureAwait(false);
    }

    private bool IsDuplicate(T key, long cutoff) =>
        _lastCheck > cutoff && _processed.TryGetValue(key, out var ticks) && ticks >= cutoff;

    private void Add(T key, long now)
    {
        if (_processed.Count == 0)
        {
            _oldest = now;
        }

        _processed.Add(key, now);
    }

    private void CleanIfNeeded(long cutoff, long now)
    {
        if (_lastCheck < cutoff)
        {
            _processed.Clear();
            _cleanedAt = now;
            _oldest = 0;
        }
        else if (_processed.Count >= 50 && _cleanedAt < _oldest)
        {
            var oldest = long.MaxValue;

            foreach (var (key, timestamp) in _processed.ToList())
            {
                if (timestamp < cutoff)
                {
                    _processed.Remove(key);
                }
                else
                {
                    oldest = Math.Min(timestamp, oldest);
                }
            }

            _cleanedAt = now;
            _oldest = oldest;
        }
    }
}