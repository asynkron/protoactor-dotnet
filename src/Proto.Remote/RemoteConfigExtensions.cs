// -----------------------------------------------------------------------
//   <copyright file="RemoteConfigExtensions.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Google.Protobuf.Reflection;
using Grpc.Core;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Remote;

[PublicAPI]
public static class RemoteConfigExtensions
{
    /// <summary>
    ///     Gets known actor kinds that can be spawned remotely
    /// </summary>
    /// <param name="remoteConfig"></param>
    /// <returns></returns>
    public static string[] GetRemoteKinds(this RemoteConfigBase remoteConfig) =>
        remoteConfig.RemoteKinds.Keys.ToArray();

    /// <summary>
    ///     Gets a specific actor kind that can be spawned remotely
    /// </summary>
    /// <param name="remoteConfig"></param>
    /// <param name="kind">Actor kind to get</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static Props GetRemoteKind(this RemoteConfigBase remoteConfig, string kind)
    {
        if (!remoteConfig.RemoteKinds.TryGetValue(kind, out var props))
        {
            throw new ArgumentException($"No Props found for kind '{kind}'");
        }

        return props;
    }

    /// <summary>
    ///     Sets the CallOptions for the gRPC channel.
    /// </summary>
    public static TRemoteConfig WithCallOptions<TRemoteConfig>(this TRemoteConfig remoteConfig, CallOptions options)
        where TRemoteConfig : RemoteConfigBase =>
        remoteConfig with { CallOptions = options };

    /// <summary>
    ///     Sets the advertised hostname for the remote system.
    ///     If the remote system is behind e.g. a NAT or reverse proxy, this needs to be set to
    ///     the external hostname in order for other systems to be able to connect to it.
    /// </summary>
    /// <param name="remoteConfig"></param>
    /// <param name="advertisedHostname"></param>
    /// <typeparam name="TRemoteConfig"></typeparam>
    /// <returns></returns>
    public static TRemoteConfig WithAdvertisedHost<TRemoteConfig>(this TRemoteConfig remoteConfig,
        string? advertisedHostname)
        where TRemoteConfig : RemoteConfigBase =>
        remoteConfig with { AdvertisedHost = advertisedHostname };

    /// <summary>
    ///     Sets the advertised port for the remote system.
    ///     If the remote system is behind e.g. a NAT or reverse proxy, this needs to be set to
    ///     the external port in order for other systems to be able to connect to it.
    ///     Advertised port can be different from the bound port, e.g. in container scenarios
    /// </summary>
    /// <param name="remoteConfig"></param>
    /// <param name="advertisedPort"></param>
    /// <returns></returns>
    public static TRemoteConfig WithAdvertisedPort<TRemoteConfig>(this TRemoteConfig remoteConfig, int? advertisedPort)
        where TRemoteConfig : RemoteConfigBase =>
        remoteConfig with { AdvertisedPort = advertisedPort };

    /// <summary>
    ///     Sets the batch size for the endpoint writer. The default value is 1000.
    ///     The endpoint writer will send up to this number of messages in a batch.
    /// </summary>
    public static TRemoteConfig WithEndpointWriterBatchSize<TRemoteConfig>(this TRemoteConfig remoteConfig,
        int endpointWriterBatchSize)
        where TRemoteConfig : RemoteConfigBase
    {
        remoteConfig.EndpointWriterOptions.EndpointWriterBatchSize = endpointWriterBatchSize;

        return remoteConfig;
    }

    /// <summary>
    ///     The number of times to retry the connection within the RetryTimeSpan, default is 8.
    /// </summary>
    public static TRemoteConfig WithEndpointWriterMaxRetries<TRemoteConfig>(this TRemoteConfig remoteConfig,
        int endpointWriterMaxRetries)
        where TRemoteConfig : RemoteConfigBase
    {
        remoteConfig.EndpointWriterOptions.MaxRetries = endpointWriterMaxRetries;

        return remoteConfig;
    }

    /// <summary>
    ///     The timespan that restarts are counted within, meaning that the retry counter resets after this timespan if no
    ///     errors.
    ///     The default value is 3 minutes.
    /// </summary>
    public static TRemoteConfig WithEndpointWriterRetryTimeSpan<TRemoteConfig>(
        this TRemoteConfig remoteConfig,
        TimeSpan endpointWriterRetryTimeSpan
    )
        where TRemoteConfig : RemoteConfigBase
    {
        remoteConfig.EndpointWriterOptions.RetryTimeSpan = endpointWriterRetryTimeSpan;

        return remoteConfig;
    }

    /// <summary>
    ///     Each retry backs off by an exponential ratio of this timespan
    ///     The default value is 100ms.
    /// </summary>
    public static TRemoteConfig WithEndpointWriterRetryBackOff<TRemoteConfig>(
        this TRemoteConfig remoteConfig,
        TimeSpan endpointWriterRetryBackoff
    )
        where TRemoteConfig : RemoteConfigBase
    {
        remoteConfig.EndpointWriterOptions.RetryBackOff = endpointWriterRetryBackoff;

        return remoteConfig;
    }

    /// <summary>
    ///     Registers file descriptors for protobuf serialization in the serialization subsystem.
    /// </summary>
    /// <param name="remoteConfig"></param>
    /// <param name="fileDescriptors">List of file descriptors to register</param>
    /// <typeparam name="TRemoteConfig"></typeparam>
    /// <returns></returns>
    /// <example>
    ///     Assuming that you have a proto file called "MyMessages.proto" and it is properly wired in the csproj file:
    ///     <code>
    /// var remoteConfig = GrpcNetRemoteConfig
    ///    .BindToAllInterfaces()
    ///    .WithProtoMessages(MyMessagesReflection.Descriptor);
    /// </code>
    /// </example>
    public static TRemoteConfig WithProtoMessages<TRemoteConfig>(this TRemoteConfig remoteConfig,
        params FileDescriptor[] fileDescriptors)
        where TRemoteConfig : RemoteConfigBase
    {
        foreach (var fd in fileDescriptors)
        {
            remoteConfig.Serialization.RegisterFileDescriptor(fd);
        }

        return remoteConfig;
    }

    /// <summary>
    ///     Registers an actor kind that can be spawned remotely.
    /// </summary>
    /// <param name="remoteConfig"></param>
    /// <param name="kind">Kind</param>
    /// <param name="prop">Props used to spawn the actor</param>
    /// <typeparam name="TRemoteConfig"></typeparam>
    /// <returns></returns>
    public static TRemoteConfig WithRemoteKind<TRemoteConfig>(this TRemoteConfig remoteConfig, string kind, Props prop)
        where TRemoteConfig : RemoteConfigBase =>
        remoteConfig with { RemoteKinds = remoteConfig.RemoteKinds.Add(kind, prop) };

    /// <summary>
    ///     Registers actor kinds that can be spawned remotely
    /// </summary>
    /// <param name="remoteConfig"></param>
    /// <param name="knownKinds">A list of tuples (kind, props used to spawn the actor)</param>
    /// <typeparam name="TRemoteConfig"></typeparam>
    /// <returns></returns>
    public static TRemoteConfig WithRemoteKinds<TRemoteConfig>(this TRemoteConfig remoteConfig,
        params (string kind, Props prop)[] knownKinds)
        where TRemoteConfig : RemoteConfigBase =>
        remoteConfig with
        {
            RemoteKinds =
            remoteConfig.RemoteKinds.AddRange(
                knownKinds.Select(kk => new KeyValuePair<string, Props>(kk.kind, kk.prop)))
        };

    /// <summary>
    ///     Adds a serializer to the serialization subsystem.
    /// </summary>
    /// <param name="remoteConfig"></param>
    /// <param name="serializerId">
    ///     Serializer id. By default 0 is the protobuf serializer and 1 is the json serializer. Use
    ///     other values for custom serializers.
    /// </param>
    /// <param name="priority">
    ///     Priority defined in which order Serializers should be considered to be the serializer for a
    ///     given type (highest value takes precedence). ProtoBufSerializer has priority of 0, and JsonSerializer has priority
    ///     of -1000.
    /// </param>
    /// <param name="serializer">Serializer to be registered</param>
    /// <typeparam name="TRemoteConfig"></typeparam>
    /// <returns></returns>
    public static TRemoteConfig WithSerializer<TRemoteConfig>(this TRemoteConfig remoteConfig, int serializerId,
        int priority, ISerializer serializer)
        where TRemoteConfig : RemoteConfigBase
    {
        remoteConfig.Serialization.RegisterSerializer(serializerId, priority, serializer);

        return remoteConfig;
    }

    /// <summary>
    ///     Sets the <see cref="JsonSerializerOptions" /> options for the JSON serializer.
    /// </summary>
    /// <param name="remoteConfig"></param>
    /// <param name="options"></param>
    /// <typeparam name="TRemoteConfig"></typeparam>
    /// <returns></returns>
    public static TRemoteConfig WithJsonSerializerOptions<TRemoteConfig>(this TRemoteConfig remoteConfig,
        JsonSerializerOptions options)
        where TRemoteConfig : RemoteConfigBase
    {
        remoteConfig.Serialization.JsonSerializerOptions = options;

        return remoteConfig;
    }

    /// <summary>
    ///     Sets the log level used when reporting deserialization errors
    /// </summary>
    /// <param name="remoteConfig"></param>
    /// <param name="level">The <see cref="LogLevel" /></param>
    /// <typeparam name="TRemoteConfig"></typeparam>
    /// <returns></returns>
    public static TRemoteConfig WithLogLevelForDeserializationErrors<TRemoteConfig>(this TRemoteConfig remoteConfig,
        LogLevel level)
        where TRemoteConfig : RemoteConfigBase =>
        remoteConfig with { DeserializationErrorLogLevel = level };

    /// <summary>
    ///     Enables remote retrieval of process information and statistics from this node
    /// </summary>
    public static TRemoteConfig WithRemoteDiagnostics<TRemoteConfig>(this TRemoteConfig remoteConfig, bool enabled)
        where TRemoteConfig : RemoteConfigBase =>
        remoteConfig with { RemoteDiagnostics = enabled };
}