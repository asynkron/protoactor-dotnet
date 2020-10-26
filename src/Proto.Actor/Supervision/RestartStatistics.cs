// -----------------------------------------------------------------------
//   <copyright file="RestartStatistics.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
// ReSharper disable once CheckNamespace
namespace Proto
{
    public class RestartStatistics
    {
        private readonly List<DateTime> _failureTimes = new List<DateTime>();

        public RestartStatistics(int failureCount, DateTime? lastFailureTime)
        {
            for (var i = 0; i < failureCount; i++)
            {
                _failureTimes.Add(lastFailureTime ?? DateTime.Now);
            }
        }

        public int FailureCount => _failureTimes.Count;

        public void Fail() => _failureTimes.Add(DateTime.Now);

        public void Reset() => _failureTimes.Clear();

        public int NumberOfFailures(TimeSpan? within) =>
            within.HasValue ? _failureTimes.Count(a => DateTime.Now - a < within) : _failureTimes.Count;
    }
}