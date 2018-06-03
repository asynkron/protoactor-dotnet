// -----------------------------------------------------------------------
//   <copyright file="RestartStatistics.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto
{
    public class RestartStatistics
    {
        private readonly List<DateTime> _failureTimes = new List<DateTime>();
        
        public int FailureCount => _failureTimes.Count;

        public RestartStatistics(int failureCount, DateTime? lastFailuretime)
        {
            for (int i = 0; i < failureCount; i++)
            {
                _failureTimes.Add(lastFailuretime ?? DateTime.Now);
            }
        }

        public void Fail() => _failureTimes.Add(DateTime.Now);

        public void Reset() => _failureTimes.Clear();

        public int NumberOfFailures(TimeSpan? within)
        {
            return within.HasValue ? _failureTimes.Count(a => DateTime.Now - a < within) : _failureTimes.Count;
        }
    }
}