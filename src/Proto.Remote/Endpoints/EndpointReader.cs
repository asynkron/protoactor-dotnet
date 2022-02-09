// -----------------------------------------------------------------------
//   <copyright file="EndpointReader.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Diagnostics;
using Proto.Remote.Metrics;

namespace Proto.Remote
{
    public class EndpointReader : Remoting.RemotingBase
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointReader>();
        private readonly EndpointManager _endpointManager;
        private readonly ActorSystem _system;

        public EndpointReader(ActorSystem system, EndpointManager endpointManager)
        {
            _system = system;
            _endpointManager = endpointManager;
        }

        public override async Task Receive(
            IAsyncStreamReader<RemoteMessage> requestStream,
            IServerStreamWriter<RemoteMessage> responseStream,
            ServerCallContext context
        )
        {
            if (_endpointManager.CancellationToken.IsCancellationRequested)
            {
                Logger.LogWarning("[{SystemAddress}] Attempt to connect to the suspended reader has been rejected", _system.Address);

                throw new RpcException(Status.DefaultCancelled, "Suspended");
            }

            using (_endpointManager.CancellationToken.Register(async () => {
                        try
                        {
                            await responseStream.WriteAsync(new RemoteMessage
                                {
                                    DisconnectRequest = new DisconnectRequest()
                                }
                            ).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            Logger.LogWarning("[{SystemAddress}] Failed to write disconnect message to the stream", _system.Address);
                        }
                    }
                ))
            {
                IEndpoint endpoint;
                string? address = null;
                string systemId;

                if (_system.Metrics.Enabled)
                {
                    RemoteMetrics.RemoteEndpointConnectedCount
                        .Add(1, new("id", _system.Id), new("address", _system.Address), new("destinationaddress", context.Peer));
                }

                Logger.LogDebug("[{SystemAddress}] Accepted connection request from {Remote} to {Local}",
                    _system.Address, context.Peer, context.Host
                );

                if (await requestStream.MoveNext().ConfigureAwait(false) &&
                    requestStream.Current.MessageTypeCase != RemoteMessage.MessageTypeOneofCase.ConnectRequest)
                    throw new RpcException(Status.DefaultCancelled, "Expected connection message");

                var connectRequest = requestStream.Current.ConnectRequest;

                var cancellationTokenSource = new CancellationTokenSource();

                switch (connectRequest.ConnectionTypeCase)
                {
                    case ConnectRequest.ConnectionTypeOneofCase.ClientConnection: {
                        var clientConnection = connectRequest.ClientConnection;

                        if (_system.Remote().BlockList.IsBlocked(clientConnection.SystemId))
                        {
                            Logger.LogWarning("[{SystemAddress}] Attempt to connect from a blocked endpoint was rejected", _system.Address);
                            await responseStream.WriteAsync(new RemoteMessage
                                {
                                    ConnectResponse = new ConnectResponse
                                    {
                                        Blocked = true,
                                        MemberId = _system.Id
                                    }
                                }
                            ).ConfigureAwait(false);
                            return;
                        }

                        await responseStream.WriteAsync(new RemoteMessage
                            {
                                ConnectResponse = new ConnectResponse
                                {
                                    MemberId = _system.Id
                                }
                            }
                        ).ConfigureAwait(false);
                        systemId = clientConnection.SystemId;
                        endpoint = _endpointManager.GetOrAddClientEndpoint(systemId);
                        _ = Task.Run(async () => {
                                try
                                {
                                    while (!cancellationTokenSource.Token.IsCancellationRequested)
                                    {
                                        while (!cancellationTokenSource.Token.IsCancellationRequested &&
                                               endpoint.OutgoingStash.TryPop(out var message))
                                        {
                                            try
                                            {
                                                await responseStream.WriteAsync(message).ConfigureAwait(false);
                                            }
                                            catch (Exception)
                                            {
                                                _ = endpoint.OutgoingStash.Append(message);
                                                throw;
                                            }
                                        }

                                        while (endpoint.OutgoingStash.IsEmpty && !cancellationTokenSource.Token.IsCancellationRequested)
                                        {
                                            var message = await endpoint.Outgoing.Reader.ReadAsync(cancellationTokenSource.Token)
                                                .ConfigureAwait(false);

                                            try
                                            {
                                                // Logger.LogInformation($"Sending {message}");
                                                await responseStream.WriteAsync(message).ConfigureAwait(false);
                                            }
                                            catch (Exception)
                                            {
                                                _ = endpoint.OutgoingStash.Append(message);
                                                throw;
                                            }
                                        }
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    Logger.LogDebug("[{SystemAddress}] Writer closed for {systemId}", _system.Address, systemId);
                                }
                                catch (Exception e)
                                {
                                    Logger.LogWarning(e, "[{SystemAddress}] Writing error to {systemId}", _system.Address, systemId);
                                }
                            }
                        );
                    }
                        break;
                    case ConnectRequest.ConnectionTypeOneofCase.ServerConnection: {
                        var serverConnection = connectRequest.ServerConnection;

                        if (_system.Remote().BlockList.IsBlocked(serverConnection.SystemId))
                        {
                            Logger.LogWarning("[{SystemAddress}] Attempt to connect from a blocked endpoint was rejected", _system.Address);
                            await responseStream.WriteAsync(new RemoteMessage
                                {
                                    ConnectResponse = new ConnectResponse
                                    {
                                        Blocked = true,
                                        MemberId = _system.Id
                                    }
                                }
                            ).ConfigureAwait(false);
                            return;
                        }

                        await responseStream.WriteAsync(new RemoteMessage
                            {
                                ConnectResponse = new ConnectResponse
                                {
                                    MemberId = _system.Id
                                }
                            }
                        ).ConfigureAwait(false);
                        address = serverConnection.Address;
                        systemId = serverConnection.SystemId;
                        endpoint = _endpointManager.GetOrAddServerEndpoint(address);
                    }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                try
                {
                    while (await requestStream.MoveNext().ConfigureAwait(false))
                    {
                        var currentMessage = requestStream.Current;
                        if (_endpointManager.CancellationToken.IsCancellationRequested)
                            continue;

                        _endpointManager.RemoteMessageHandler.HandleRemoteMessage(currentMessage);
                    }
                }
                finally
                {
                    cancellationTokenSource.Cancel();
                    if (address is null && systemId is not null)
                        _system.EventStream.Publish(new EndpointTerminatedEvent(false, null, systemId));

                    if (_system.Metrics.Enabled)
                    {
                        RemoteMetrics.RemoteEndpointDisconnectedCount
                            .Add(1, new("id", _system.Id), new("address", _system.Address), new("destinationaddress", context.Peer));
                    }
                }
            }
        }

        public override Task<ListProcessesResponse> ListProcesses(ListProcessesRequest request, ServerCallContext context)
        {
            if (!_system.Remote().Config.RemoteDiagnostics)
            {
                throw new Exception("RemoteDiagnostics is not enabled");
            }

            switch (request.Type)
            {
                case ListProcessesMatchType.MatchPartOfString: {
                    var pids = _system.ProcessRegistry.Find(request.Pattern).ToArray();
                    return Task.FromResult(new ListProcessesResponse()
                        {
                            Pids = {pids}
                        }
                    );
                }
                case ListProcessesMatchType.MatchExactString: {
                    var pids = _system.ProcessRegistry.Find(id => id == request.Pattern).ToArray();
                    return Task.FromResult(new ListProcessesResponse()
                        {
                            Pids = {pids}
                        }
                    );
                }
                case ListProcessesMatchType.MatchRegex: {
                    var regex = new Regex(request.Pattern);
                
                    var pids = _system.ProcessRegistry.Find(id => regex.IsMatch(id)).ToArray();
                    return Task.FromResult(new ListProcessesResponse()
                        {
                            Pids = {pids}
                        }
                    );
                }
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public override async Task<GetProcessDiagnosticsResponse> GetProcessDiagnostics(
            GetProcessDiagnosticsRequest request,
            ServerCallContext context
        )
        {
            if (!_system.Remote().Config.RemoteDiagnostics)
            {
                throw new Exception("RemoteDiagnostics is not enabled");
            }

            var res = await DiagnosticTools.GetDiagnosticsString(_system, request.Pid).ConfigureAwait(false);
            return new GetProcessDiagnosticsResponse()
            {
                DiagnosticsString = res
            };
        }
    }
}