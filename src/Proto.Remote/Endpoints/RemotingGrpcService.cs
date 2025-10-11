// -----------------------------------------------------------------------
//   <copyright file="RemotingGrpcService.cs" company="Asynkron AB">
//       Copyright (C) 2015-2025 Asynkron AB All rights reserved
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

public sealed class RemotingGrpcService : Remoting.RemotingBase
{
    private static readonly ILogger Logger = Log.CreateLogger<RemotingGrpcService>();
    private readonly EndpointManager _endpointManager;
    private readonly ActorSystem _system;

    private readonly record struct NegotiationResult(IRemoteEndpoint Endpoint, string SystemId, string? Address, bool StartWriter);

    public RemotingGrpcService(ActorSystem system, EndpointManager endpointManager)
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
                "[RemotingGrpcService][{SystemAddress}] Attempt to connect to the suspended reader has been rejected",
                _system.Address);

            throw new RpcException(Status.DefaultCancelled, "Suspended");
        }

        var cancellationTokenSource = new CancellationTokenSource();

        await using (
            _endpointManager.CancellationToken.Register(() =>
            {
                _ = RemoteStreamProcessor.DisconnectAsync(responseStream, cancellationTokenSource);
            }).ConfigureAwait(false))
        {
            Logger.LogInformation(
                "[RemotingGrpcService][{SystemAddress}] Accepted connection request from {Remote} to {Local}",
                _system.Address, context.Peer, context.Host
            );

            var negotiation = await NegotiateAsync(requestStream, responseStream).ConfigureAwait(false);
            if (negotiation is null)
            {
                return;
            }

            var (endpoint, systemId, address, startWriter) = negotiation.Value;

            if (startWriter)
            {
                _ = Task.Run(async () =>
                {
                    await RunClientWriter(responseStream, cancellationTokenSource, endpoint, systemId).ConfigureAwait(false);
                });
            }

        await RunReader(requestStream, address, cancellationTokenSource, systemId).ConfigureAwait(false);
        }
    }

    private async Task<NegotiationResult?> NegotiateAsync(
        IAsyncStreamReader<RemoteMessage> requestStream,
        IServerStreamWriter<RemoteMessage> responseStream)
    {
        if (await requestStream.MoveNext(_endpointManager.CancellationToken).ConfigureAwait(false) &&
            requestStream.Current.MessageTypeCase != RemoteMessage.MessageTypeOneofCase.ConnectRequest)
        {
            throw new RpcException(Status.DefaultCancelled, "Expected connection message");
        }

        var connectRequest = requestStream.Current.ConnectRequest;
        return connectRequest.ConnectionTypeCase switch
        {
            ConnectRequest.ConnectionTypeOneofCase.ClientConnection =>
                await HandleClientConnectionAsync(connectRequest.ClientConnection, responseStream).ConfigureAwait(false),
            ConnectRequest.ConnectionTypeOneofCase.ServerConnection =>
                await HandleServerConnectionAsync(connectRequest.ServerConnection, responseStream).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private async Task<NegotiationResult?> HandleClientConnectionAsync(
        ClientConnection clientConnection,
        IServerStreamWriter<RemoteMessage> responseStream)
    {
        if (_system.Remote().BlockList.IsBlocked(clientConnection.MemberId))
        {
            Logger.LogWarning(
                "[RemotingGrpcService][{SystemAddress}] Attempt to connect from a blocked endpoint was rejected",
                _system.Address);

            await responseStream.WriteAsync(new RemoteMessage
            {
                ConnectResponse = new ConnectResponse
                {
                    Blocked = true,
                    MemberId = _system.Id
                }
            }).ConfigureAwait(false);

            return null;
        }

        await responseStream.WriteAsync(new RemoteMessage
        {
            ConnectResponse = new ConnectResponse
            {
                MemberId = _system.Id
            }
        }).ConfigureAwait(false);

        var systemId = clientConnection.MemberId;
        var endpoint = _endpointManager.GetOrAddClientEndpoint(systemId);
        return new NegotiationResult(endpoint, systemId, null, true);
    }

    private async Task<NegotiationResult?> HandleServerConnectionAsync(
        ServerConnection serverConnection,
        IServerStreamWriter<RemoteMessage> responseStream)
    {
        var shouldExit = false;
        var blocked = serverConnection.BlockList.ToHashSet();

        if (_system.Remote().BlockList.IsBlocked(serverConnection.MemberId))
        {
            Logger.LogWarning(
                "[RemotingGrpcService][{SystemAddress}] Connection Refused from remote member {MemberId} address {Address}, they are blocked",
                _system.Address, serverConnection.MemberId,
                serverConnection.Address);

            await responseStream.WriteAsync(new RemoteMessage
            {
                ConnectResponse = new ConnectResponse
                {
                    Blocked = true,
                    MemberId = _system.Id
                }
            }).ConfigureAwait(false);

            shouldExit = true;
        }

        if (blocked.Contains(_system.Id))
        {
            Logger.LogWarning(
                "[RemotingGrpcService][{SystemAddress}] Connection Refused from remote member {MemberId} address {Address}, we are blocked",
                _system.Address, serverConnection.MemberId,
                serverConnection.Address);

            shouldExit = true;
        }

        if (blocked.Any())
        {
            _system.Remote().BlockList.Block(blocked, "Blocked by remote member");
        }

        if (shouldExit)
        {
            return null;
        }

        await responseStream.WriteAsync(new RemoteMessage
        {
            ConnectResponse = new ConnectResponse
            {
                MemberId = _system.Id
            }
        }).ConfigureAwait(false);

        var address = serverConnection.Address;
        var systemId = serverConnection.MemberId;
        var endpoint = _endpointManager.GetOrAddServerEndpoint(address);
        if (!endpoint.IsActive)
        {
            Logger.LogWarning(
                "[RemotingGrpcService][{SystemAddress}] Failed to connect back to remote member {MemberId} address {Address} for writes",
                _system.Address, serverConnection.MemberId,
                serverConnection.Address);
        }

        return new NegotiationResult(endpoint, systemId, address, false);
    }

    private async Task RunReader(IAsyncStreamReader<RemoteMessage> requestStream, string? address,
        CancellationTokenSource cancellationTokenSource, string systemId)
    {
        try
        {
            await RemoteStreamProcessor.RunReaderAsync(
                requestStream,
                cancellationTokenSource.Token,
                currentMessage =>
                {
                    if (_endpointManager.CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    _endpointManager.RemoteMessageHandler.HandleRemoteMessage(currentMessage, address!);
                }).ConfigureAwait(false);
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

    private async Task RunClientWriter(IServerStreamWriter<RemoteMessage> responseStream,
        CancellationTokenSource cancellationTokenSource, IRemoteEndpoint endpoint, string systemId)
    {
        try
        {
            await RemoteStreamProcessor.RunWriterAsync(
                endpoint,
                _system,
                _system.Remote().Config,
                (m, ct) => responseStream.WriteAsync(m),
                cancellationTokenSource.Token,
                cancellationTokenSource,
                null).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("[RemotingGrpcService][{SystemAddress}] Writer closed for {SystemId}", _system.Address,
                systemId);
        }
        catch (Exception e)
        {
            e.CheckFailFast();

            Logger.LogWarning(e, "[RemotingGrpcService][{SystemAddress}] Writing error to {SystemId}", _system.Address,
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