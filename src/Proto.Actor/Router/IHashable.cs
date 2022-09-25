// -----------------------------------------------------------------------
// <copyright file="IHashable.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router;

/// <summary>
///     Adds a capability to a message to return a hash key. Used in conjunction with routers.
/// </summary>
public interface IHashable
{
    /// <summary>
    ///     Return the hash key for this message.
    /// </summary>
    /// <returns></returns>
    string HashBy();
}