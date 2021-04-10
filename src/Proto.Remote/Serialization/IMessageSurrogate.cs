// -----------------------------------------------------------------------
// <copyright file="IMessageSurrogate.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Remote
{
    /// <summary>
    /// The root level in-process representation of a message
    /// </summary>
    public interface IRootSerializable
    {
        /// <summary>
        /// Returns the on-the-wire representation of the message
        /// 
        /// Message -> IRootSerialized -> ByteString
        /// </summary>
        /// <param name="system">The ActorSystem the message belongs to</param>
        /// <returns></returns>
        IRootSerialized Serialize(ActorSystem system);
    }
    
    /// <summary>
    /// The root level on-the-wire representation of a message
    /// </summary>
    public interface IRootSerialized
    {
        /// <summary>
        /// Returns the in-process representation of a message
        ///
        /// ByteString -> IRootSerialized -> Message
        /// </summary>
        /// <param name="system">The ActorSystem the message belongs to</param>
        /// <returns></returns>
        IRootSerializable Deserialize(ActorSystem system);
    }
}