#!/bin/bash

dotnet publish -c Release Node1
dotnet publish -c Release Node2
docker-compose up --build
