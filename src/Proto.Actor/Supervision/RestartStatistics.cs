// -----------------------------------------------------------------------
// <copyright file="RestartStatistics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
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

        public int NumberOfFailures(TimeSpan? within)
        {
            if (!within.HasValue)
                return _failureTimes.Count;
            var result = 0;
            foreach (var failureTime in _failureTimes)
            {
                if (DateTimeOffset.UtcNow - failureTime < within)
                    result++;
            }

            return result;
        }
    }
}