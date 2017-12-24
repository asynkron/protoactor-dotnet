#!/bin/bash -e

dotnet publish -c Release ../ClusterHelloWorld/Node1
dotnet publish -c Release ../ClusterHelloWorld/Node2
docker-compose up --build
