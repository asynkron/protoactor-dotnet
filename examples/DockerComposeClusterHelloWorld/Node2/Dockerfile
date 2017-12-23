FROM microsoft/aspnetcore-build:2.0 

ADD ./bin/Release/netcoreapp2.0/publish/ /app

WORKDIR /app
EXPOSE 12000
CMD [ "dotnet", "Node2.dll" ]
