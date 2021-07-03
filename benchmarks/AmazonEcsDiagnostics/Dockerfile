FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["KubernetesDiagnostics.csproj", "/src"]
RUN dotnet restore "/src/KubernetesDiagnostics.csproj"
COPY . .
WORKDIR /src
RUN dotnet build "/src/KubernetesDiagnostics.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KubernetesDiagnostics.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KubernetesDiagnostics.dll"]