
docker build -f dockerfile-reader . -t rogeralsing/clusterreader
docker build -f dockerfile-sender . -t rogeralsing/clustersender

kubectl run mongo --image=mongo:4 --expose --port 27017
kubectl port-forward service/mongo 27017:27017
kubectl apply --filename service.yaml