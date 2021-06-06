kubectl delete --filename service.yaml
docker build . -t rogeralsing/kubdiagg:default -f Dockerfile
docker push docker.io/rogeralsing/kubdiagg:default
kubectl apply --filename service.yaml