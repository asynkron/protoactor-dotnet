// -----------------------------------------------------------------------
// <copyright file="IMessageAutoAck.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto
{
    public interface IAutoRespond
    {
        object GetAutoResponse(IContext context);
    }
}