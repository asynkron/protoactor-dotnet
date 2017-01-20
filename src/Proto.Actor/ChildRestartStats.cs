using System;
using System.Collections.Generic;
using System.Text;

namespace Proto
{
    public class ChildRestartStats
    {
        public ChildRestartStats(int failureCount,DateTime? lastFailuretime)
        {
            FailureCount = failureCount;
            LastFailureTime = lastFailuretime;
        }
        public int FailureCount { get; }
        public DateTime? LastFailureTime { get; }


        public bool RequestRestartPermission(int maxNrOfRetries, TimeSpan? withinTimeSpan)
        {
            if (maxNrOfRetries == 0)
            {
                return false;
            }

            if (withinTimeSpan == null)
            {
                return FailureCount <= maxNrOfRetries;
            }

            //TODO: implement timespan check
            return FailureCount <= maxNrOfRetries;

            return true;
        }
    }
}
