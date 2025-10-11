FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["benchmarks/EndpointManagerTest/EndpointManagerTest.csproj", "benchmarks/EndpointManagerTest/"]
COPY ["src/Proto.Actor/Proto.Actor.csproj", "src/Proto.Actor/"]
COPY ["src/Proto.Remote/Proto.Remote.csproj", "src/Proto.Remote/"]
RUN dotnet restore "benchmarks/EndpointManagerTest/EndpointManagerTest.csproj"
COPY . .
WORKDIR "/src/benchmarks/EndpointManagerTest"
RUN dotnet build "EndpointManagerTest.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EndpointManagerTest.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EndpointManagerTest.dll"]
