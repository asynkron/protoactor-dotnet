#!/bin/bash -e

sed -e "s/{{project_id}}/$PROJECT_ID/g" ./manifests/app/node_template.yaml | sed -e "s/{{port}}/12001/g" > sed -e "s/{{name}}/node1/g" > ./manifests/app/node1.yaml
sed -e "s/{{project_id}}/$PROJECT_ID/g" ./manifests/app/node_template.yaml | sed -e "s/{{port}}/12000/g" > sed -e "s/{{name}}/node2/g" > ./manifests/app/node2.yaml

set +e
kubectl get namespace test
if [ $? -eq 1 ]
then
    echo "Namespace missing, creating"
    kubectl create namespace test
else
    echo "Namespace test already exists"
fi

set -e
kubectl create -f manifests/consul/service.yaml --namespace=test
kubectl create -f manifests/consul/statefulsets.yaml --namespace=test
kubectl create -f manifests/app/node1.yaml --namespace=test
kubectl create -f manifests/app/node2.yaml --namespace=test
