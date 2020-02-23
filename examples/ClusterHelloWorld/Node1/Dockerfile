FROM microsoft/aspnetcore-build:2.0 

ADD ./bin/Release/netcoreapp3.1/publish/ /app

WORKDIR /app
EXPOSE 12001
ENTRYPOINT [ "dotnet", "Node1.dll" ]