// -----------------------------------------------------------------------
// <copyright file = "ForceSerializationSenderMiddleware.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace Proto.Remote;

public static class ForcedSerializationSenderMiddleware
{
    private static readonly ILogger Logger = Log.CreateLogger(nameof(ForcedSerializationSenderMiddleware));

    /// <summary>
    ///     Returns sender middleware that forces serialization of the message. This middleware serializes and then
    ///     deserializes the message before
    ///     sending it further down the pipeline. It simulates the serialization process in <see cref="Endpoint" />.
    ///     Useful for testing if serialization is working correctly and the messages are immutable.
    /// </summary>
    /// <param name="shouldSerialize">
    ///     A predicate that can prevent serialization by returning false.
    ///     If null, it defaults to <see cref="SkipInternalProtoMessages" />
    /// </param>
    /// <returns>
    ///     Middleware configuration function, to be used with WithSenderMiddleware on
    ///     <see cref="Props" /> or on <see cref="RootContext" /> configuration
    /// </returns>
    public static Func<Sender, Sender> Create(Func<Proto.MessageEnvelope, bool>? shouldSerialize = null)
    {
        shouldSerialize ??= SkipInternalProtoMessages;

        return next =>
            (context, target, envelope) =>
            {
                object? message = null;
                PID? sender;
                Proto.MessageHeader headers;

                try
                {
                    if (shouldSerialize?.Invoke(envelope) == false)
                    {
                        return next(context, target, envelope);
                    }

                    var serialization = context.System.Serialization();

                    // serialize
                    (message, sender, headers) = Proto.MessageEnvelope.Unwrap(envelope);

                    if (message is IRootSerializable rootSerializable)
                    {
                        message = rootSerializable.Serialize(context.System);
                    }

                    if (message is null)
                    {
                        throw new Exception("Null message passed to the forced serialization middleware");
                    }

                    var (bytes, typeName, serializerId) = serialization.Serialize(message);

                    // deserialize
                    var deserializedMessage = serialization.Deserialize(typeName, bytes, serializerId);

                    if (message is IRootSerialized rootDeserialized)
                    {
                        deserializedMessage = rootDeserialized.Deserialize(context.System);
                    }

                    // forward
                    var newEnvelope = new Proto.MessageEnvelope(deserializedMessage, sender, headers);

                    return next(context, target, newEnvelope);
                }
                catch (CodedOutputStream.OutOfSpaceException oom)
                {
                    Logger.LogError(oom, "Message is too large for serialization {Message}", message?.GetType().Name);

                    throw;
                }
                catch (Exception ex)
                {
                    ex.CheckFailFast();

                    Logger.LogError(ex, "Forced serialization -> deserialization failed for message {Message}",
                        message?.GetType().Name);

                    throw;
                }
            };
    }

    /// <summary>
    ///     Predicate to skip serialization of internal Proto messages
    /// </summary>
    /// <param name="envelope"></param>
    /// <returns></returns>
    public static bool SkipInternalProtoMessages(Proto.MessageEnvelope envelope)
    {
        var (message, _, _) = Proto.MessageEnvelope.Unwrap(envelope);

        return message.GetType().FullName?.StartsWith("Proto.") == false;
    }
}