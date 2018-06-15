using OpenTracing.Tag;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.OpenTracing
{
    public static class ProtoTags
    {
        public static readonly StringTag MessageType = new StringTag("proto.messagetype");

        public static readonly StringTag TargetPID = new StringTag("proto.targetpid");

        public static readonly StringTag SenderPID = new StringTag("proto.senderpid");

        public static readonly StringTag ActorType = new StringTag("proto.actortype"); // TODO ?

        //public static readonly StringTag ActorKind = new StringTag("proto.actorkind"); // TODO ?
    }
}
