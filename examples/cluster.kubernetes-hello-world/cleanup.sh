#!/bin/bash

kubectl delete statefulsets consul --namespace=test
kubectl delete service consul --namespace=test
kubectl delete service node1 --namespace=test
kubectl delete pod node1 --namespace=test
kubectl delete service node2 --namespace=test
kubectl delete pod node2 --namespace=test
kubectl delete namespace test
