using OpenTracing.Tag;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.OpenTracing
{
    public static class ProtoTags
    {
        /// <summary>
        /// GetType().Name on the message
        /// </summary>
        public static readonly StringTag MessageType = new StringTag("proto.messagetype");

        /// <summary>
        /// Message destination
        /// </summary>
        public static readonly StringTag TargetPID = new StringTag("proto.targetpid");
        /// <summary>
        /// Message origin
        /// </summary>
        public static readonly StringTag SenderPID = new StringTag("proto.senderpid");
        /// <summary>
        /// Current actor PID, when applicable (equals TargetPID when this is a receive span, or SenderId when this is a sending span)
        /// </summary>
        public static readonly StringTag ActorPID = new StringTag("proto.actorpid");

        /// <summary>
        /// Type of the current actor, when applicable
        /// </summary>
        public static readonly StringTag ActorType = new StringTag("proto.actortype");



        //public static readonly StringTag ActorKind = new StringTag("proto.actorkind"); // TODO ? How ? it's in the PID so ... it's ok
    }
}
