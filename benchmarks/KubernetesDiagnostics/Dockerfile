FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["benchmarks/KubernetesDiagnostics/KubernetesDiagnostics.csproj", "KubernetesDiagnostics/"]
RUN dotnet restore "benchmarks/KubernetesDiagnostics/KubernetesDiagnostics.csproj"
COPY . .
WORKDIR "/src/KubernetesDiagnostics"
RUN dotnet build "KubernetesDiagnostics.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KubernetesDiagnostics.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KubernetesDiagnostics.dll"]
