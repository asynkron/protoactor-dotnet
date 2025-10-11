docker login
kubectl delete --filename service.yaml
dotnet publish --os linux -c Release --arch arm64 -p:PublishProfile=DefaultContainer 
docker tag kubernetesdiagnostics:1.0.0 rogeralsing/kubediag
docker push rogeralsing/kubediag
kubectl apply --filename service.yaml