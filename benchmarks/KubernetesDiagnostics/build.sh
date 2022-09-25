kubectl delete --filename service.yaml
dotnet publish --os linux -c Release --arch x64 -p:PublishProfile=DefaultContainer
kubectl apply --filename service.yaml