// -----------------------------------------------------------------------
// <copyright file="ResponseStatusCode.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Remote
{
    public enum ResponseStatusCode
    {
        OK,
        Unavailable,
        Timeout,
        ProcessNameAlreadyExist,
        Error
    }
}