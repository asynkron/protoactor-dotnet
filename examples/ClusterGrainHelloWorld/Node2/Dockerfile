FROM mcr.microsoft.com/dotnet/sdk:5.0 AS base
COPY . .
WORKDIR ./examples/ClusterGrainHelloWorld/Node2
RUN dotnet publish -c Release -r linux-x64 Node2.csproj

FROM mcr.microsoft.com/dotnet/runtime:5.0 as publish
COPY --from=base ./examples/ClusterGrainHelloWorld/Node2/bin/Release/net5.0/linux-x64/publish /app

WORKDIR /app
EXPOSE 12001
ENTRYPOINT [ "dotnet", "Node2.dll" ]