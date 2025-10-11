// -----------------------------------------------------------------------
//   <copyright file="RemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2025 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Grpc.Core;
using Grpc.Net.Client;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace Proto.Remote;

[PublicAPI]
public record RemoteConfig
{
    public const string AllInterfaces = "0.0.0.0";
    public const string Localhost = "127.0.0.1";
    public const int AnyFreePort = 0;

    private RemoteConfig(string host, int port)
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
    ///     After the remote connection is terminated, this is the time period the endpoint manager will monitor messages
    ///     arriving to this connection
    ///     and generate deadletter events for them. Default value is 3 seconds.
    /// </summary>
    public TimeSpan? WaitAfterEndpointTerminationTimeSpan { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     Enables remote retrieval of process information and statistics from this node
    /// </summary>
    public bool RemoteDiagnostics { get; set; }

    /// <summary>
    ///     Set to true to enable SSL on the channel
    /// </summary>
    public bool UseHttps { get; init; }

    /// <summary>
    ///     Channel options for the gRPC channel
    /// </summary>
    [JsonIgnore]
    public GrpcChannelOptions ChannelOptions { get; init; } = new();

    /// <summary>
    ///     A delegate that performs additional configuration on Kestrel <see cref="ListenOptions" />.
    ///     If not supplied, the default implementation sets the protocol to HTTP2
    /// </summary>
    [JsonIgnore]
    public Action<ListenOptions>? ConfigureKestrel { get; init; }

    /// <summary>
    ///     A delegate that allows to choose the address for the <see cref="ActorSystem" /> from the list of addresses Kestrel
    ///     listens on.
    ///     By default, the first address is used.
    /// </summary>
    [JsonIgnore]
    public Func<IEnumerable<Uri>?, Uri?> UriChooser { get; init; } = uris => uris?.FirstOrDefault();

    /// <summary>
    ///     Creates new <see cref="RemoteConfig" /> instance that binds to all network interfaces
    /// </summary>
    /// <param name="advertisedHost">
    ///     The advertised hostname for the remote system.
    ///     If the remote system is behind e.g. a NAT or reverse proxy, this needs to be set to
    ///     the external hostname in order for other systems to be able to connect to it.
    /// </param>
    /// <param name="port">Port to bind on, 0 (default) means random port</param>
    /// <returns></returns>
    public static RemoteConfig BindToAllInterfaces(string? advertisedHost = null, int port = 0) =>
        new RemoteConfig(AllInterfaces, port).WithAdvertisedHost(advertisedHost);

    /// <summary>
    ///     Creates new <see cref="RemoteConfig" /> instance that binds to a loopback interface. Useful for local
    ///     development.
    /// </summary>
    /// <param name="port">Port to bind on, 0 (default) means random port</param>
    /// <returns></returns>
    public static RemoteConfig BindToLocalhost(int port = 0) => new(Localhost, port);

    /// <summary>
    ///     Creates new <see cref="RemoteConfig" /> instance that binds to a specific network interface.
    /// </summary>
    /// <param name="host">Host to bind to</param>
    /// <param name="port">Port to bind on, 0 (default) means random port</param>
    /// <returns></returns>
    public static RemoteConfig BindTo(string host, int port = 0) => new(host, port);
}