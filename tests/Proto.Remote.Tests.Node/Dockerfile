ARG BUILDER_IMG=mcr.microsoft.com/dotnet/core/sdk:3.1.102
ARG RUNNER_IMG=mcr.microsoft.com/dotnet/core/aspnet:3.1.0-alpine3.10

FROM $BUILDER_IMG AS builder

WORKDIR /app

# copy csproj and restore as distinct layers
COPY . .
RUN dotnet restore -nowarn:msb3202,nu1503 tests/Proto.Remote.Tests.Node -r linux-musl-x64

# copy everything else, build and publish the final binaries
RUN dotnet publish tests/Proto.Remote.Tests.Node -c Release -r linux-musl-x64 --no-restore --no-self-contained -clp:NoSummary -o /app/publish /p:PublishReadyToRun=false,PublishSingleFile=false

# Create final runtime image
FROM $RUNNER_IMG AS runner

RUN echo 'http://dl-cdn.alpinelinux.org/alpine/v3.8/main' >> /etc/apk/repositories && \
    apk update --no-cache && \
    apk add --no-cache bash libc6-compat=1.1.19-r11

WORKDIR /app
COPY --from=builder /app/publish .

EXPOSE 12000
ENTRYPOINT ["dotnet", "Proto.Remote.Tests.Node.dll"]
