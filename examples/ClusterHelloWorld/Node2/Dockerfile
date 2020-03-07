FROM mcr.microsoft.com/dotnet/core/aspnet:3.1.0-alpine3.10

RUN echo 'http://dl-cdn.alpinelinux.org/alpine/v3.8/main' >> /etc/apk/repositories && \
    apk update --no-cache && \
    apk add --no-cache bash libc6-compat=1.1.19-r11

ADD ./bin/Release/netcoreapp3.1/linux-musl-x64/publish/ /app

WORKDIR /app
EXPOSE 12000
ENTRYPOINT [ "dotnet", "Node2.dll" ]
