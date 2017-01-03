using System;

namespace Proto
{
    public class Failure : SystemMessage
    {
        private Exception reason;
        private PID who;

        public Failure(PID who, Exception reason)
        {
            this.who = who;
            this.reason = reason;
        }
    }
}