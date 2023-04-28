// -----------------------------------------------------------------------
// <copyright file = "PubSubDeliveryException.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Cluster.PubSub;

#pragma warning disable RCS1194
public class PubSubDeliveryException : Exception
#pragma warning restore RCS1194
{
    public PubSubDeliveryException(string? message) : base(message)
    {
    }

    public PubSubDeliveryException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}