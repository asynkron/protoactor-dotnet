// -----------------------------------------------------------------------
// <copyright file="RestartStatistics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public class RestartStatistics
    {
        private readonly List<DateTimeOffset> _failureTimes = new();

        public RestartStatistics(int failureCount, DateTimeOffset? lastFailureTime)
        {
            for (var i = 0; i < failureCount; i++)
            {
                _failureTimes.Add(lastFailureTime ?? DateTimeOffset.UtcNow);
            }
        }

        public int FailureCount => _failureTimes.Count;

        public void Fail() => _failureTimes.Add(DateTimeOffset.UtcNow);

        public void Reset() => _failureTimes.Clear();

        public int NumberOfFailures(TimeSpan? within) =>
            within.HasValue ? _failureTimes.Count(a => DateTimeOffset.UtcNow - a < within) : _failureTimes.Count;
    }
}