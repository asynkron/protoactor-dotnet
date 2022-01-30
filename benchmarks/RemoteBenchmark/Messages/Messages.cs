// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;
using Proto.Remote;

namespace Messages
{
    public partial class Ping : ICachedSerialization
    {
    }
    
    public partial class Pong : ICachedSerialization
    {
    }
}