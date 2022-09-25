// -----------------------------------------------------------------------
// <copyright file="ISerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using Google.Protobuf;

namespace Proto.Remote;

/// <summary>
///     Abstraction for message serialization
/// </summary>
public interface ISerializer
{
    /// <summary>
    ///     Serializes a message
    /// </summary>
    /// <param name="obj">Message to be serialized</param>
    /// <returns>Message bytes</returns>
    ByteString Serialize(object obj);

    /// <summary>
    ///     Deserializes a message
    /// </summary>
    /// <param name="bytes">Message bytes to be deserialized</param>
    /// <param name="typeName">Message type name</param>
    /// <returns></returns>
    object Deserialize(ByteString bytes, string typeName);

    /// <summary>
    ///     Retrieves a type name for a message. Type name is used during deserialization. It is the the responsibility of
    ///     the <see cref="ISerializer" /> implementation to maintain a mapping between type names and messages.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    string GetTypeName(object message);

    /// <summary>
    ///     Returns true if this serializer can serialize specified message
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    bool CanSerialize(object obj);
}