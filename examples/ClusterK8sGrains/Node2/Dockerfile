FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["examples/ClusterK8sGrains/Node1/Node1.csproj", "examples/ClusterK8sGrains/Node1/"]
COPY ["examples/ClusterK8sGrains/Node2/Node2.csproj", "examples/ClusterK8sGrains/Node2/"]
COPY ["src/Proto.Cluster.Kubernetes/Proto.Cluster.Kubernetes.csproj", "src/Proto.Cluster.Kubernetes/"]
COPY ["src/Proto.Cluster/Proto.Cluster.csproj", "src/Proto.Cluster/"]
COPY ["src/Proto.Remote/Proto.Remote.csproj", "src/Proto.Remote/"]
COPY ["src/Proto.Actor/Proto.Actor.csproj", "src/Proto.Actor/"]
COPY ["examples/ClusterK8sGrains/Messages/Messages.csproj", "examples/ClusterK8sGrains/Messages/"]
RUN dotnet restore "examples/ClusterK8sGrains/Node1/Node1.csproj"
RUN dotnet restore "examples/ClusterK8sGrains/Node2/Node2.csproj"
COPY . .
WORKDIR "/src/examples/ClusterK8sGrains/Node2"
RUN dotnet build "Node2.csproj" -c $BUILD_CONFIGURATION -o /app/build 

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Node2.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Node2.dll"]
