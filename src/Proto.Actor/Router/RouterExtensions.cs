// -----------------------------------------------------------------------
// <copyright file="RouterExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Proto.Router.Routers;

namespace Proto.Router;

public static class RouterExtensions
{
    /// <summary>
    ///     Creates props for a router, that broadcasts messages to all of its routees.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="routees">List of routee <see cref="PID" /></param>
    /// <returns></returns>
    public static Props NewBroadcastGroup(this ISenderContext senderContext, params PID[] routees) =>
        new BroadcastGroupRouterConfig(senderContext, routees).Props();

    /// <summary>
    ///     Creates props for a router, that routes the messages to the routees by calculating the hash of the message key and
    ///     finding a routee on a hash ring.
    ///     The message has to implement <see cref="IHashable" />. Uses <see cref="MurmurHash2" /> as hash function.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="routees">List of routee <see cref="PID" /></param>
    /// <returns></returns>
    public static Props NewConsistentHashGroup(this ISenderContext senderContext, params PID[] routees) =>
        new ConsistentHashGroupRouterConfig(senderContext, MurmurHash2.Hash, 100, null, routees).Props();

    /// <summary>
    ///     Creates props for a router, that routes the messages to the routees by calculating the hash of the message key and
    ///     finding a routee on a hash ring.
    ///     If the message is <see cref="IHashable" />, then the key extracted with <see cref="IHashable.HashBy" /> takes
    ///     precedence. Otherwise, the hash key
    ///     is extracted from the message with a provided delegate.
    ///     Uses <see cref="MurmurHash2" /> as hash function.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="messageHasher">Gets the message and returns a hash key for it.</param>
    /// <param name="routees">List of routee <see cref="PID" />List of routee <see cref="PID" /></param>
    /// <returns></returns>
    public static Props NewConsistentHashGroup(
        this ISenderContext senderContext,
        Func<object, string> messageHasher,
        params PID[] routees
    ) =>
        new ConsistentHashGroupRouterConfig(senderContext, MurmurHash2.Hash, 100, messageHasher, routees)
            .Props();

    /// <summary>
    ///     Creates props for a router, that routes the messages to the routees by calculating the hash of the message key and
    ///     finding a routee on a hash ring.
    ///     The message has to implement <see cref="IHashable" />.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="hash">Hashing function</param>
    /// <param name="replicaCount">Number of virtual copies of the routee PID on the hash ring</param>
    /// <param name="routees">List of routee <see cref="PID" /></param>
    /// <returns></returns>
    public static Props NewConsistentHashGroup(
        this ISenderContext senderContext,
        Func<string, uint> hash,
        int replicaCount,
        params PID[] routees
    ) =>
        new ConsistentHashGroupRouterConfig(senderContext, hash, replicaCount, null, routees).Props();

    /// <summary>
    ///     Creates props for a router, that routes the messages to the routees by calculating the hash of the message key and
    ///     finding a routee on a hash ring.
    ///     If the message is <see cref="IHashable" />, then the key extracted with <see cref="IHashable.HashBy" /> takes
    ///     precedence. Otherwise, the hash key
    ///     is extracted from the message with a provided delegate.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="hash">Hashing function</param>
    /// <param name="replicaCount">Number of virtual copies of the routee on the hash ring</param>
    /// <param name="messageHasher">Gets the message and returns a hash key for it.</param>
    /// <param name="routees">List of routee <see cref="PID" /></param>
    /// <returns></returns>
    public static Props NewConsistentHashGroup(
        this ISenderContext senderContext,
        Func<string, uint> hash,
        int replicaCount,
        Func<object, string>? messageHasher,
        params PID[] routees
    ) =>
        new ConsistentHashGroupRouterConfig(senderContext, hash, replicaCount, messageHasher, routees).Props();

    /// <summary>
    ///     Creates props for a router, that routes messages to a random routee.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="routees">List of routee <see cref="PID" /></param>
    /// <returns></returns>
    public static Props NewRandomGroup(this ISenderContext senderContext, params PID[] routees) =>
        new RandomGroupRouterConfig(senderContext, routees).Props();

    /// <summary>
    ///     Creates props for a router, that routes messages to a random routee.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="seed">Random seed</param>
    /// <param name="routees">List of routee <see cref="PID" /></param>
    /// <returns></returns>
    public static Props NewRandomGroup(this ISenderContext senderContext, int seed, params PID[] routees) =>
        new RandomGroupRouterConfig(senderContext, seed, routees).Props();

    /// <summary>
    ///     Creates props for a router, that routes messages to its routees in a round robin fashion.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="routees">List of routee <see cref="PID" /></param>
    /// <returns></returns>
    public static Props NewRoundRobinGroup(this ISenderContext senderContext, params PID[] routees) =>
        new RoundRobinGroupRouterConfig(senderContext, routees).Props();

    /// <summary>
    ///     Creates props for a router that broadcasts the message to all the actors in the pool it maintains.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="props">Props to spawn actors - members of the pool</param>
    /// <param name="poolSize">Size of the pool</param>
    /// <returns></returns>
    public static Props NewBroadcastPool(this ISenderContext senderContext, Props props, int poolSize) =>
        new BroadcastPoolRouterConfig(senderContext, poolSize, props).Props();

    /// <summary>
    ///     Creates props for a router, that routes the messages to the pool of actors it maintains by calculating the hash of
    ///     the message key and finding a pool member on a hash ring.
    ///     If the message is <see cref="IHashable" />, then the key extracted with <see cref="IHashable.HashBy" /> takes
    ///     precedence. Otherwise, the hash key
    ///     is extracted from the message with a provided delegate.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="props">Props to spawn actors - members of the pool</param>
    /// <param name="poolSize">Size of the pool</param>
    /// <param name="hash">Hashing function</param>
    /// <param name="replicaCount">Number of virtual copies of the pool member on the hash ring</param>
    /// <param name="messageHasher">Gets the message and returns a hash key for it.</param>
    /// <returns></returns>
    public static Props NewConsistentHashPool(
        this ISenderContext senderContext,
        Props props,
        int poolSize,
        Func<string, uint>? hash = null,
        int replicaCount = 100,
        Func<object, string>? messageHasher = null
    ) =>
        new ConsistentHashPoolRouterConfig(senderContext, poolSize, props, hash ?? MurmurHash2.Hash,
                replicaCount, messageHasher
            )
            .Props();

    /// <summary>
    ///     Creates props for a router, that routes messages to a random member of the pool it maintains.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="props">Props to spawn actors - members of the pool</param>
    /// <param name="poolSize">Size of the pool</param>
    /// <param name="seed">Random seed</param>
    /// <returns></returns>
    public static Props NewRandomPool(
        this ISenderContext senderContext,
        Props props,
        int poolSize,
        int? seed = null
    ) =>
        new RandomPoolRouterConfig(senderContext, poolSize, props, seed).Props();

    /// <summary>
    ///     Creates props for a router, that routes messages to member of the pool it maintains in a round robin fashion.
    /// </summary>
    /// <param name="senderContext">Context to send the messages through</param>
    /// <param name="props">Props to spawn actors - members of the pool</param>
    /// <param name="poolSize">Size of the pool</param>
    /// <returns></returns>
    public static Props NewRoundRobinPool(this ISenderContext senderContext, Props props, int poolSize) =>
        new RoundRobinPoolRouterConfig(senderContext, poolSize, props).Props();
}