FROM microsoft/aspnetcore-build:2.0 

ADD ./bin/Release/netcoreapp2.0/publish/ /app

WORKDIR /app
EXPOSE 12000
ENTRYPOINT [ "dotnet", "Node2.dll" ]
