# Proto Remote Tests Context

## Overview
Unit and integration tests for the remote transport and endpoint handling.

_Parent context: [Tests](../context.md)_

## Key Files
* `AssemblyInfo.cs` – C# source defining Assembly Info behavior.
* `ConnectionFailTests.cs` – C# source defining Connection Fail Tests behavior.
* `DisplayTestMethodNameAttribute.cs` – C# source defining Display Test Method Name Attribute behavior.
* `EchoActor.cs` – C# source defining Echo Actor behavior.
* `ForcedSerializationTests.cs` – C# source defining Forced Serialization Tests behavior.
* `LargeMessageEnvelopeTests.cs` – C# source defining Large Message Envelope Tests behavior.
* `Proto.Remote.Tests.csproj` – Project file configuring compilation targets and dependencies.
* `RemoteFixture.cs` – C# source defining Remote Fixture behavior.
* `RemoteKindsRegistrationTests.cs` – C# source defining Remote Kinds Registration Tests behavior.
* `RemoteStreamProcessorTests.cs` – C# source defining Remote Stream Processor Tests behavior.
* `RemoteTests.cs` – C# source defining Remote Tests behavior.
* `SerializationTests.cs` – C# source defining Serialization Tests behavior.

## Primary Types and Contracts
* `Class DisplayTestMethodNameAttribute` defined in `DisplayTestMethodNameAttribute.cs`
* `Class EchoActor` defined in `EchoActor.cs`
* `Class ForcedSerializationTests` defined in `ForcedSerializationTests.cs`
* `Record TestMessage` defined in `ForcedSerializationTests.cs`
* `Record TestRootSerializableMessage` defined in `ForcedSerializationTests.cs`
* `Record TestRootSerializedMessage` defined in `ForcedSerializationTests.cs`
* `Record TestResponse` defined in `ForcedSerializationTests.cs`
* `Record RunRequest` defined in `ForcedSerializationTests.cs`
* `Record RunRequestAsync` defined in `ForcedSerializationTests.cs`
* `Class LargeMessageEnvelopeTests` defined in `LargeMessageEnvelopeTests.cs`
* `Interface IRemoteFixture` defined in `RemoteFixture.cs`
* `Class RemoteFixture` defined in `RemoteFixture.cs`
* `Class RemoteKindsRegistrationTests` defined in `RemoteKindsRegistrationTests.cs`
* `Class RemoteStreamProcessorTests` defined in `RemoteStreamProcessorTests.cs`
* `Class TestEndpoint` defined in `RemoteStreamProcessorTests.cs`
* `Class TestWriter` defined in `RemoteStreamProcessorTests.cs`
* `Class TestReader` defined in `RemoteStreamProcessorTests.cs`
* `Class ThrowingAfterFirstReader` defined in `RemoteStreamProcessorTests.cs`
* `Class RemoteTests` defined in `RemoteTests.cs`
* `Class SerializationTests` defined in `SerializationTests.cs`
* `Class TestType1` defined in `SerializationTests.cs`
* `Class TestType2` defined in `SerializationTests.cs`
* `Record JsonMessage` defined in `SerializationTests.cs`
* `Class MockSerializer1` defined in `SerializationTests.cs`
* `Class MockSerializer2` defined in `SerializationTests.cs`

## Related Subcontexts
* [Remote Tests](RemoteTests/context.md) – See the nested context for details.

