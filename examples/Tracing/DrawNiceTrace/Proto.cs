using Proto;
using System;
using System.Collections.Generic;
using System.Text;

namespace DrawNiceTrace
{
    public partial class TriggerFood
    {
        /// <summary>
        /// TODO Couldn't find how to import types from the original proto
        /// Protos.proto:60:5: "actor.PID" is not defined.
        /// Import "actor" was not found or had errors.
        /// Protos.proto:58:5: "PID" is not defined.
        /// </summary>
        public PID Customer { get; set; }
    }

    public partial class SendPaymentDetails
    {
        /// <summary>
        /// Same
        /// </summary>
        public PID Customer { get; set; }
    }
}
