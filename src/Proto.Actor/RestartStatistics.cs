// -----------------------------------------------------------------------
//  <copyright file="RestartStatistics.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto
{
    public class RestartStatistics
    {
        public RestartStatistics(int failureCount, DateTime? lastFailuretime)
        {
            FailureCount = failureCount;
            LastFailureTime = lastFailuretime;
        }

        public int FailureCount { get; set; }
        public DateTime? LastFailureTime { get; set; }


        public bool RequestRestartPermission(int maxNrOfRetries, TimeSpan? withinTimeSpan)
        {
            if (maxNrOfRetries == 0)
            {
                return false;
            }

            FailureCount++;

            //supervisor says child may restart, and we don't care about any timewindow
            if (withinTimeSpan == null)
            {
                return FailureCount <= maxNrOfRetries;
            }

            var max = DateTime.Now - withinTimeSpan;
            if (LastFailureTime > max)
            {
                return FailureCount <= maxNrOfRetries;
            }

            //we are past the time limit, we can safely reset the failure count and restart
            FailureCount = 0;
            return true;
        }
    }
}