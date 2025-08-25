namespace Proto.Remote;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Proto.Extensions;

/// <summary>
/// Helpers for processing remote gRPC streams.
/// </summary>
internal static class RemoteStreamProcessor
{
    /// <summary>
    /// Reads all messages from the given stream and forwards them to the handler.
    /// </summary>
    public static async Task RunReaderAsync(
        IAsyncStreamReader<RemoteMessage> reader,
        CancellationToken token,
        Action<RemoteMessage> onMessage)
    {
        try
        {
            while (await reader.MoveNext(token).ConfigureAwait(false))
            {
                onMessage(reader.Current);
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Cancelled)
        {
            // expected when the remote disconnects
        }
        catch (InvalidOperationException)
        {
            // thrown if MoveNext is invoked after the request is complete
        }
    }

    /// <summary>
    /// Writes outgoing message batches for an endpoint to the provided stream writer.
    /// </summary>
    public static async Task RunWriterAsync(
        IEndpoint endpoint,
        ActorSystem system,
        RemoteConfig remoteConfig,
        Func<RemoteMessage, CancellationToken, Task> write,
        CancellationToken token,
        CancellationTokenSource cts,
        Action<double>? recordWriteDuration)
    {
        while (!token.IsCancellationRequested)
        {
            while (endpoint.OutgoingStash.TryPop(out var messages))
            {
                var batch = MessageBatchFactory.CreateBatch(system, remoteConfig, messages);
                try
                {
                    var sw = Stopwatch.StartNew();
                    await write(new RemoteMessage { MessageBatch = batch }, token).ConfigureAwait(false);
                    sw.Stop();
                    recordWriteDuration?.Invoke(sw.Elapsed.TotalSeconds);
                }
                catch (Exception)
                {
                    endpoint.OutgoingStash.Push(messages);
                    cts.Cancel();
                    throw;
                }
            }

            await foreach (var messages in endpoint.Outgoing.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                var batch = MessageBatchFactory.CreateBatch(system, remoteConfig, messages);
                try
                {
                    var sw = Stopwatch.StartNew();
                    await write(new RemoteMessage { MessageBatch = batch }, token).ConfigureAwait(false);
                    sw.Stop();
                    recordWriteDuration?.Invoke(sw.Elapsed.TotalSeconds);
                }
                catch (Exception)
                {
                    endpoint.OutgoingStash.Push(messages);
                    cts.Cancel();
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Sends a disconnect message and cancels the associated token source.
    /// </summary>
    public static async Task DisconnectAsync(
        IServerStreamWriter<RemoteMessage> responseStream,
        CancellationTokenSource cts)
    {
        try
        {
            await responseStream.WriteAsync(new RemoteMessage
            {
                DisconnectRequest = new DisconnectRequest()
            }).ConfigureAwait(false);
        }
        catch (Exception x)
        {
            x.CheckFailFast();
        }
        finally
        {
            cts.Cancel();
        }
    }

    /// <summary>
    /// Cancels the token source and completes the request stream.
    /// </summary>
    public static async Task CompleteAsync(
        AsyncDuplexStreamingCall<RemoteMessage, RemoteMessage> call,
        CancellationTokenSource cts)
    {
        cts.Cancel();
        await call.RequestStream.CompleteAsync().ConfigureAwait(false);
    }
}

