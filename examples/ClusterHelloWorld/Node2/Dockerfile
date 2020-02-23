FROM microsoft/aspnetcore-build:2.0 

ADD ./bin/Release/netcoreapp3.1/publish/ /app

WORKDIR /app
EXPOSE 12000
ENTRYPOINT [ "dotnet", "Node2.dll" ]
