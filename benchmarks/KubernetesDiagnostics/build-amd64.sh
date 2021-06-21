kubectl delete --filename service.yaml
docker build . -t rogeralsing/kubdiagg:amd64 -f Dockerfile-amd64
docker push docker.io/rogeralsing/kubdiagg:amd64
kubectl apply --filename service-amd64.yaml --namespace rogerroger