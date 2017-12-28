#!/bin/bash -e

kubectl port-forward consul-0 8500:8500 --namespace=test
