#!/bin/bash -e

dotnet publish -c Release ../ClusterHelloWorld/Node1
dotnet publish -c Release ../ClusterHelloWorld/Node2

docker build -t gcr.io/${PROJECT_ID}/protodemonode1:v1 ../ClusterHelloWorld/Node1/.
docker build -t gcr.io/${PROJECT_ID}/protodemonode2:v1 ../ClusterHelloWorld/Node2/.

gcloud docker -- push gcr.io/${PROJECT_ID}/protodemonode1:v1
gcloud docker -- push gcr.io/${PROJECT_ID}/protodemonode2:v1