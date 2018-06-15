using OpenTracing.Tag;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.OpenTracing
{
    public static class ProtoTags
    {
        public static readonly StringTag TargetPID = new StringTag("proto.targetpid");
        public static readonly StringTag SenderPID = new StringTag("proto.senderpid");

        
    }
}
