rm -rf bin/Debug
dotnet build
dotnet pack
rm -rf ../../../../protoactor-dotnet-grainreference/artifacts/
mkdir ../../../../protoactor-dotnet-grainreference/artifacts
cp bin/Debug/*.nupkg ../../../../protoactor-dotnet-grainreference/artifacts
rm -rf ~/.nuget/packages/protograingenerator
dotnet restore ../../../../protoactor-dotnet-grainreference
