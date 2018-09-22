
protoc Protos.proto -I=. -I=../../../src --csharp_out=. --csharp_opt=file_extension=.g.cs
dotnet ..\..\..\protobuf\ProtoGrainGenerator\bin\Debug\netcoreapp2.0\protograin.dll Protos.proto Protos_protoactor.cs