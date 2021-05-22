// -----------------------------------------------------------------------
//   <copyright file="RemoteConfigExtensions.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Reflection;
using Grpc.Core;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    [PublicAPI]
    public static class RemoteConfigExtensions
    {
        public static string[] GetRemoteKinds(this RemoteConfigBase remoteConfig)
            => remoteConfig.RemoteKinds.Keys.ToArray();

        public static Props GetRemoteKind(this RemoteConfigBase remoteConfig, string kind)
        {
            if (!remoteConfig.RemoteKinds.TryGetValue(kind, out var props)) throw new ArgumentException($"No Props found for kind '{kind}'");

            return props;
        }

        public static TRemoteConfig WithCallOptions<TRemoteConfig>(this TRemoteConfig remoteConfig, CallOptions options)
            where TRemoteConfig : RemoteConfigBase =>
            remoteConfig with {CallOptions = options};

        public static TRemoteConfig WithAdvertisedHost<TRemoteConfig>(this TRemoteConfig remoteConfig, string? advertisedHostname)
            where TRemoteConfig : RemoteConfigBase =>
            remoteConfig with {AdvertisedHost = advertisedHostname};

        /// <summary>
        ///     Advertised port can be different from the bound port, e.g. in container scenarios
        /// </summary>
        /// <param name="advertisedPort"></param>
        /// <returns></returns>
        public static TRemoteConfig WithAdvertisedPort<TRemoteConfig>(this TRemoteConfig remoteConfig, int? advertisedPort)
            where TRemoteConfig : RemoteConfigBase =>
            remoteConfig with {AdvertisedPort = advertisedPort};

        public static TRemoteConfig WithEndpointWriterBatchSize<TRemoteConfig>(this TRemoteConfig remoteConfig, int endpointWriterBatchSize)
            where TRemoteConfig : RemoteConfigBase
        {
            remoteConfig.EndpointWriterOptions.EndpointWriterBatchSize = endpointWriterBatchSize;
            return remoteConfig;
        }

        public static TRemoteConfig WithEndpointWriterMaxRetries<TRemoteConfig>(this TRemoteConfig remoteConfig, int endpointWriterMaxRetries)
            where TRemoteConfig : RemoteConfigBase
        {
            remoteConfig.EndpointWriterOptions.MaxRetries = endpointWriterMaxRetries;
            return remoteConfig;
        }

        public static TRemoteConfig WithEndpointWriterRetryTimeSpan<TRemoteConfig>(
            this TRemoteConfig remoteConfig,
            TimeSpan endpointWriterRetryTimeSpan
        )
            where TRemoteConfig : RemoteConfigBase
        {
            remoteConfig.EndpointWriterOptions.RetryTimeSpan = endpointWriterRetryTimeSpan;
            return remoteConfig;
        }

        public static TRemoteConfig WithEndpointWriterRetryBackOff<TRemoteConfig>(
            this TRemoteConfig remoteConfig,
            TimeSpan endpointWriterRetryBackoff
        )
            where TRemoteConfig : RemoteConfigBase
        {
            remoteConfig.EndpointWriterOptions.RetryBackOff = endpointWriterRetryBackoff;
            return remoteConfig;
        }

        public static TRemoteConfig WithProtoMessages<TRemoteConfig>(this TRemoteConfig remoteConfig, params FileDescriptor[] fileDescriptors)
            where TRemoteConfig : RemoteConfigBase
        {
            foreach (var fd in fileDescriptors)
            {
                remoteConfig.Serialization.RegisterFileDescriptor(fd);
            }

            return remoteConfig;
        }

        public static TRemoteConfig WithRemoteKind<TRemoteConfig>(this TRemoteConfig remoteConfig, string kind, Props prop)
            where TRemoteConfig : RemoteConfigBase =>
            remoteConfig with {RemoteKinds = remoteConfig.RemoteKinds.Add(kind, prop)};

        public static TRemoteConfig WithRemoteKinds<TRemoteConfig>(this TRemoteConfig remoteConfig, params (string kind, Props prop)[] knownKinds)
            where TRemoteConfig : RemoteConfigBase =>
            remoteConfig with
            {
                RemoteKinds =
                remoteConfig.RemoteKinds.AddRange(knownKinds.Select(kk => new KeyValuePair<string, Props>(kk.kind, kk.prop)))
            };

        public static TRemoteConfig WithSerializer<TRemoteConfig>(this TRemoteConfig remoteConfig, int serializerId, int priority, ISerializer serializer)
            where TRemoteConfig : RemoteConfigBase
        {
            remoteConfig.Serialization.RegisterSerializer(serializerId, priority, serializer);
            return remoteConfig;
        }

        public static TRemoteConfig WithJsonSerializerOptions<TRemoteConfig>(this TRemoteConfig remoteConfig, System.Text.Json.JsonSerializerOptions options)
            where TRemoteConfig : RemoteConfigBase
        {
            remoteConfig.Serialization.JsonSerializerOptions = options;
            return remoteConfig;
        }

        public static TRemoteConfig WithLogLevelForDeserializationErrors<TRemoteConfig>(this TRemoteConfig remoteConfig, LogLevel level)
            where TRemoteConfig : RemoteConfigBase =>
            remoteConfig with {DeserializationErrorLogLevel = level};
        
        public static TRemoteConfig WithRemoteDiagnostics<TRemoteConfig>(this TRemoteConfig remoteConfig,bool enabled)
            where TRemoteConfig : RemoteConfigBase =>
            remoteConfig with {RemoteDiagnostics = enabled};
    }
}