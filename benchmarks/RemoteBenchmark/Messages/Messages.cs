// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;
using Proto.Remote;

namespace Messages
{
    public partial class CachedPing : ICachedSerialization
    {
    }

    public partial class CachedPong : ICachedSerialization
    {
    }

    public class PingClr
    {
    }

    public class PongClr
    {
    }
    public class CachedPingClr : ICachedSerialization
    {
    }

    public class CachedPongClr : ICachedSerialization
    {
    }
}