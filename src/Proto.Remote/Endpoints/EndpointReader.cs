// -----------------------------------------------------------------------
//   <copyright file="EndpointReader.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Diagnostics;

namespace Proto.Remote;

public sealed class EndpointReader : Remoting.RemotingBase
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
            Logger.LogWarning(
                "[EndpointReader][{SystemAddress}] Attempt to connect to the suspended reader has been rejected",
                _system.Address);

            throw new RpcException(Status.DefaultCancelled, "Suspended");
        }

        async void Disconnect()
        {
            try
            {
                var disconnectMsg = new RemoteMessage
                {
                    DisconnectRequest = new DisconnectRequest()
                };

                await responseStream.WriteAsync(disconnectMsg).ConfigureAwait(false);
            }
            catch (Exception x)
            {
                x.CheckFailFast();

                Logger.LogWarning("[EndpointReader][{SystemAddress}] Failed to write disconnect message to the stream",
                    _system.Address);
            }
        }

        await using (_endpointManager.CancellationToken.Register(Disconnect).ConfigureAwait(false))
        {
            IEndpoint endpoint;
            string? address = null;
            string systemId;

            Logger.LogInformation(
                "[EndpointReader][{SystemAddress}] Accepted connection request from {Remote} to {Local}",
                _system.Address, context.Peer, context.Host
            );

            if (await requestStream.MoveNext(_endpointManager.CancellationToken).ConfigureAwait(false) &&
                requestStream.Current.MessageTypeCase != RemoteMessage.MessageTypeOneofCase.ConnectRequest)
            {
                throw new RpcException(Status.DefaultCancelled, "Expected connection message");
            }

            var connectRequest = requestStream.Current.ConnectRequest;

            var cancellationTokenSource = new CancellationTokenSource();

            switch (connectRequest.ConnectionTypeCase)
            {
                case ConnectRequest.ConnectionTypeOneofCase.ClientConnection:
                {
                    var clientConnection = connectRequest.ClientConnection;

                    if (_system.Remote().BlockList.IsBlocked(clientConnection.MemberId))
                    {
                        Logger.LogWarning(
                            "[EndpointReader][{SystemAddress}] Attempt to connect from a blocked endpoint was rejected",
                            _system.Address);

                        await responseStream.WriteAsync(new RemoteMessage
                                {
                                    ConnectResponse = new ConnectResponse
                                    {
                                        Blocked = true,
                                        MemberId = _system.Id
                                    }
                                }
                            )
                            .ConfigureAwait(false);

                        return;
                    }

                    await responseStream.WriteAsync(new RemoteMessage
                            {
                                ConnectResponse = new ConnectResponse
                                {
                                    MemberId = _system.Id
                                }
                            }
                        )
                        .ConfigureAwait(false);

                    systemId = clientConnection.MemberId;
                    endpoint = _endpointManager.GetOrAddClientEndpoint(systemId);

                    _ = Task.Run(async () =>
                    {
                        await RunClientWriter(responseStream, cancellationTokenSource, endpoint, systemId).ConfigureAwait(false);
                    });
                }

                    break;
                case ConnectRequest.ConnectionTypeOneofCase.ServerConnection:
                {
                    var serverConnection = connectRequest.ServerConnection;
                    var shouldExit = false;
                    var blocked = serverConnection.BlockList.ToHashSet();

                    if (_system.Remote().BlockList.IsBlocked(serverConnection.MemberId))
                    {
                        Logger.LogWarning(
                            "[EndpointReader][{SystemAddress}] Connection Refused from remote member {MemberId} address {Address}, they are blocked",
                            _system.Address, connectRequest.ServerConnection.MemberId,
                            connectRequest.ServerConnection.Address);

                        await responseStream.WriteAsync(new RemoteMessage
                                {
                                    ConnectResponse = new ConnectResponse
                                    {
                                        Blocked = true,
                                        MemberId = _system.Id
                                    }
                                }
                            )
                            .ConfigureAwait(false);

                        shouldExit = true;
                    }

                    if (blocked.Contains(_system.Id))
                    {
                        Logger.LogWarning(
                            "[EndpointReader][{SystemAddress}] Connection Refused from remote member {MemberId} address {Address}, we are blocked",
                            _system.Address, connectRequest.ServerConnection.MemberId,
                            connectRequest.ServerConnection.Address);

                        shouldExit = true;
                    }

                    if (blocked.Any())
                    {
                        _system.Remote().BlockList.Block(blocked);
                    }

                    if (shouldExit)
                    {
                        return;
                    }

                    await responseStream.WriteAsync(new RemoteMessage
                            {
                                ConnectResponse = new ConnectResponse
                                {
                                    MemberId = _system.Id
                                }
                            }
                        )
                        .ConfigureAwait(false);

                    address = serverConnection.Address;
                    systemId = serverConnection.MemberId;
                    endpoint = _endpointManager.GetOrAddServerEndpoint(address);
                }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            await RunReader(requestStream, address, cancellationTokenSource, systemId).ConfigureAwait(false);
        }
    }

    private async Task RunReader(IAsyncStreamReader<RemoteMessage> requestStream, string? address,
        CancellationTokenSource cancellationTokenSource, string systemId)
    {
        try
        {
            while (await requestStream.MoveNext(CancellationToken.None).ConfigureAwait(false))
            {
                var currentMessage = requestStream.Current;

                if (_endpointManager.CancellationToken.IsCancellationRequested)
                {
                    continue;
                }

                _endpointManager.RemoteMessageHandler.HandleRemoteMessage(currentMessage, address!);
            }
        }
        finally
        {
            cancellationTokenSource.Cancel();

            if (address is null && systemId is not null)
            {
                _system.EventStream.Publish(new EndpointTerminatedEvent(false, null, systemId));
            }
        }
    }

    private async Task RunClientWriter(IAsyncStreamWriter<RemoteMessage> responseStream,
        CancellationTokenSource cancellationTokenSource, IEndpoint endpoint, string systemId)
    {
        try
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                //consume stash
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

                //
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
            Logger.LogDebug("[EndpointReader][{SystemAddress}] Writer closed for {SystemId}", _system.Address,
                systemId);
        }
        catch (Exception e)
        {
            e.CheckFailFast();

            Logger.LogWarning(e, "[EndpointReader][{SystemAddress}] Writing error to {SystemId}", _system.Address,
                systemId);
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
            case ListProcessesMatchType.MatchPartOfString:
            {
                var pids = _system.ProcessRegistry.Find(request.Pattern).ToArray();

                return Task.FromResult(new ListProcessesResponse
                    {
                        Pids = { pids }
                    }
                );
            }
            case ListProcessesMatchType.MatchExactString:
            {
                var pids = _system.ProcessRegistry.Find(id => id == request.Pattern).ToArray();

                return Task.FromResult(new ListProcessesResponse
                    {
                        Pids = { pids }
                    }
                );
            }
            case ListProcessesMatchType.MatchRegex:
            {
                var regex = new Regex(request.Pattern);

                var pids = _system.ProcessRegistry.Find(id => regex.IsMatch(id)).ToArray();

                return Task.FromResult(new ListProcessesResponse
                    {
                        Pids = { pids }
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

        return new GetProcessDiagnosticsResponse
        {
            DiagnosticsString = res
        };
    }
}