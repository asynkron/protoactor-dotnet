// -----------------------------------------------------------------------
//   <copyright file="RemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Grpc.Core;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Remote;

[PublicAPI]
public abstract record RemoteConfigBase
{
    public const string AllInterfaces = "0.0.0.0";
    public const string Localhost = "127.0.0.1";
    public const int AnyFreePort = 0;

    protected RemoteConfigBase(string host, int port)
    {
        Host = host;
        Port = port;
    }

    /// <summary>
    ///     The host to listen to
    /// </summary>
    public string Host { get; }

    /// <summary>
    ///     The port to listen to, 0 means any free port
    /// </summary>
    public int Port { get; }

    /// <summary>
    ///     Known actor kinds that can be spawned remotely
    /// </summary>
    [JsonIgnore]
    public ImmutableDictionary<string, Props> RemoteKinds { get; init; } =
        ImmutableDictionary<string, Props>.Empty;

    /// <summary>
    ///     Gets or sets the CallOptions for the gRPC channel.
    /// </summary>
    [JsonIgnore]
    public CallOptions CallOptions { get; init; }

    /// <summary>
    ///     Gets or sets the advertised hostname for the remote system.
    ///     If the remote system is behind e.g. a NAT or reverse proxy, this needs to be set to
    ///     the external hostname in order for other systems to be able to connect to it.
    /// </summary>
    public string? AdvertisedHost { get; init; }

    /// <summary>
    ///     Gets or sets the advertised port for the remote system.
    ///     If the remote system is behind e.g. a NAT or reverse proxy, this needs to be set to
    ///     the external port in order for other systems to be able to connect to it.
    ///     Advertised port can be different from the bound port, e.g. in container scenarios
    /// </summary>
    public int? AdvertisedPort { get; init; }

    /// <summary>
    ///     Gets or sets logging level for deserialization errors
    ///     Defaults to Error.
    /// </summary>
    public LogLevel DeserializationErrorLogLevel { get; init; } = LogLevel.Error;

    /// <summary>
    ///     Endpoint writer options
    /// </summary>
    [JsonIgnore]
    public EndpointWriterOptions EndpointWriterOptions { get; init; } = new();

    /// <summary>
    ///     Serializations system that manages serializers for remote messages.
    /// </summary>
    [JsonIgnore]
    public Serialization Serialization { get; init; } = new();

    /// <summary>
    ///     After the remote connection is terminated, this is the time period the enpoint manager will monitor messages
    ///     arriving to this connection
    ///     and generate deadletter events for them. Default value is 3 seconds.
    /// </summary>
    public TimeSpan? WaitAfterEndpointTerminationTimeSpan { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     Enables remote retrieval of process information and statistics from this node
    /// </summary>
    public bool RemoteDiagnostics { get; set; }
}