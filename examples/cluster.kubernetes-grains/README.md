Example of a Proto.Actor cluster running in k8s with the kuberneties service locator.
Is also using k8s DNS name, to support the use of TLS between the cluster members. 

# Setup
Was build and testing using k3s and Rancher Desktop and registry for local development.

## Local registry (optional)
If you don't already have container repository, you can setup a local registry for loading and testing this example.
```shell
docker volume create registry_data
docker run -d -p 5000:5000 --name registry --restart=always -v registry_data:/var/lib/registry registry:2
```

## Build and push images

Run from the root of the repository to build and push the images to the local registry.
```shell
docker build -t protoactor.example.k8s.grains.node1:latest -f .\examples\ClusterK8sGrains\Node1\Dockerfile .
docker tag protoactor.example.k8s.grains.node1:latest localhost:5000/protoactor.example.k8s.grains.node1:latest
docker push localhost:5000/protoactor.example.k8s.grains.node1:latest

docker build -t protoactor.example.k8s.grains.node2:latest -f .\examples\ClusterK8sGrains\Node2\Dockerfile .
docker tag protoactor.example.k8s.grains.node2:latest localhost:5000/protoactor.example.k8s.grains.node2:latest
docker push localhost:5000/protoactor.example.k8s.grains.node2:latest
```

## Deploy to k8s

From the ClusterK8sGrains folder run the following commands to deploy the application to k8s.
```shell
helm install protoactor-example-k8s-grains .\examples\ClusterK8sGrains\chart
```

To remove the application from k8s run the following command.
```shell
helm uninstall protoactor-example-k8s-grains
```