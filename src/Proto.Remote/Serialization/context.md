# Serialization Context

## Overview
Message serialization abstractions, message descriptors, and serializers for remote messaging.

_Parent context: [Proto Remote](../context.md)_

## Key Files
* `ForcedSerializationSenderMiddleware.cs` – C# source defining Forced Serialization Sender Middleware behavior.
* `ICachedSerialization.cs` – C# source defining I Cached Serialization behavior.
* `IMessageSurrogate.cs` – C# source defining I Message Surrogate behavior.
* `ISerializer.cs` – C# source defining I Serializer behavior.
* `JsonSerializer.cs` – C# source defining Json Serializer behavior.
* `ProtobufSerializer.cs` – C# source defining Protobuf Serializer behavior.
* `Serialization.cs` – C# source defining Serialization behavior.

## Primary Types and Contracts
* `Class ForcedSerializationSenderMiddleware` defined in `ForcedSerializationSenderMiddleware.cs`
* `Interface ICachedSerialization` defined in `ICachedSerialization.cs`
* `Interface IRootSerializable` defined in `IMessageSurrogate.cs`
* `Interface IRootSerialized` defined in `IMessageSurrogate.cs`
* `Interface ISerializer` defined in `ISerializer.cs`
* `Class JsonSerializer` defined in `JsonSerializer.cs`
* `Class ProtobufSerializer` defined in `ProtobufSerializer.cs`
* `Class Serialization` defined in `Serialization.cs`
* `Struct SerializerItem` defined in `Serialization.cs`
* `Struct TypeSerializerItem` defined in `Serialization.cs`

## Related Subcontexts
* No nested subcontexts.

