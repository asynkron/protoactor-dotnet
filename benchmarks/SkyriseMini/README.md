# .NET virtual actor frameworks benchmark

This benchmark compares messaging throughput and activation time of popular .NET virtual actor frameworks: Orleans, Proto.Actor, Akka.Net, Dapr.

## Environment setup

### Local tools

1. Azure CLI
2. Docker
3. helm
4. Dapr CLI and local env (install, then run with `dapr run --app-id myapp --app-port 5071 --dapr-http-port 3500`)

### Azure

(You can use ARM template in the [azure](azure) directory as a reference)

Create following Azure resources:
1. Azure Storage Account - connection string is required for Orleans clustering
2. Azure Service Bus (Standard) - used to for communication between Test Runner and Test Runner client
3. Azure Container Registry - it will store Docker images
4. Azure Kubernetes Service - environment for running the tests
   1. 2 node system pool, B2ms, label: `test-role=management`
   2. 3 node user pool, D4, label: `test-role=sut`
   3. 3 node user pool, D4, label: `test-role=runner`
   4. RBAC enabled
   5. No availability zones
   6. No Azure monitoring
   7. Be sure to connect it with the ACR upon creation

Get credentials for AKS, e.g.

```shell
az aks list -o table

# Name    Location     ResourceGroup    KubernetesVersion    ProvisioningState    Fqdn
# ------  -----------  ---------------  -------------------  -------------------  ---------------------------------------------
# ab-k8s  northeurope  ActorBenchmark   1.22.6               Succeeded            ab-k8s-dns-9a630584.hcp.northeurope.azmk8s.io

az aks get-credentials -n ab-k8s -g ActorBenchmark
```

## Supporting services in k8s

### Seq
Used for central logging. 

```shell
helm repo add datalust https://helm.datalust.co
helm repo update
helm install my-seq -f kubernetes/values-seq.yaml -n seq --create-namespace datalust/seq
```

You can connect to seq by forwarding a port, e.g.:
```shell
kubectl port-forward service/my-seq -n seq 5341:80
```

Then open http://localhost:5241

### Prometheus

```shell
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update
helm install -n prometheus --create-namespace -f kubernetes/values-prometheus.yaml my-prometheus prometheus-community/prometheus
```

### Grafana

```shell
helm repo add grafana https://grafana.github.io/helm-charts
helm repo update
helm install -n grafana --create-namespace -f kubernetes/values-grafana.yaml my-grafana grafana/grafana
```

You can connect to Grafana by forwarding a port, e.g.:

```shell
kubectl port-forward service/my-grafana -n grafana 3000:80
```

Then open http://localhost:3000

## Deploy benchmark to k8s

This walkthrough assumes the container registry is named `abimgregistry.azurecr.io`. If you're using a different name, update the `docker/image-*.sh` and `kubernetes/deployment-*.yaml` scripts.


### Prepare the images

Log into the image registry on your machine, e.g.:

```shell
az acr list -o table

# NAME           RESOURCE GROUP    LOCATION     SKU    LOGIN SERVER              CREATION DATE         ADMIN ENABLED
# -------------  ----------------  -----------  -----  ------------------------  --------------------  ---------------
# abimgregistry  ActorBenchmark    northeurope  Basic  abimgregistry.azurecr.io  2022-03-25T07:55:04Z  False

az acr login -n abimgregistry 
```

Run these scripts to build and push images. 

```shell
cd docker
./image-orleans-sut.sh
./image-proto-actor-sut.sh
./image-akka-sut.sh
./image-dapr-sut.sh
./image-test-runner.sh
```

### Prepare namespace 

```shell
kubectl create namespace benchmark
```

### Orleans tests

#### Orleans SUT

Replace Azure storage connection string in `kubernetes/deployment-orleans-sut.yaml`

```shell
kubectl apply -n benchmark -f kubernetes/deployment-orleans-sut.yaml 
```

#### Test runner - for Orleans test

*Note: Only one configuration of test runner can be deployed at a time.*

Replace Azure Service bus connection string and Azure storage connection string in `kubernetes/deployment-test-runner-orleans.yaml`

```shell
kubectl apply -n benchmark -f kubernetes/deployment-test-runner-orleans.yaml
```

### Proto.Actor tests

#### Proto.Actor SUT

```shell
kubectl apply -n benchmark -f kubernetes/deployment-proto-actor-sut.yaml 
```

#### Test runner - for Proto.Actor test

*Note: Only one configuration of test runner can be deployed at a time.*

Replace Azure Service bus connection string string in `kubernetes/deployment-test-runner-proto-actor.yaml`

```shell
kubectl apply -n benchmark -f kubernetes/deployment-test-runner-proto-actor.yaml
```

### Akka tests

#### Lighthouse

Akka.net Lighthouse is used as seed node provider.

```shell
kubectl apply -n benchmark -f kubernetes/statefulset-lighthouse.yml
```

#### Akka SUT

```shell
kubectl apply -n benchmark -f kubernetes/deployment-akka-sut.yaml
```

#### Test runner - for Akka test

*Note: Only one configuration of test runner can be deployed at a time.*

Replace Azure Service bus connection string in `kubernetes/deployment-test-runner-akka.yaml`

```shell
kubectl apply -n benchmark -f kubernetes/deployment-test-runner-akka.yaml
```

### Dapr tests

#### Dapr environment

You'll need a Redis deployment
```shell
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo update
helm install -n redis --create-namespace -f kubernetes/values-redis.yaml my-redis bitnami/redis
```

You'll also need to install Dapr in the cluster
```shell
helm repo add dapr https://dapr.github.io/helm-charts/
helm repo update
helm install -n dapr --create-namespace -f kubernetes/values-dapr.yaml my-dapr dapr/dapr
kubectl apply -n benchmark -f kubernetes/dapr-state-store.yaml
kubectl apply -n benchmark -f kubernetes/dapr-app-config.yaml
```

#### Dapr SUT

```shell
kubectl apply -n benchmark -f kubernetes/deployment-dapr-sut.yaml
```

#### Test runner - for Dapr test

*Note: Only one configuration of test runner can be deployed at a time.*

Replace Azure Service bus connection string in `kubernetes/deployment-test-runner-akka.yaml`

```shell
kubectl apply -n benchmark -f kubernetes/deployment-test-runner-dapr.yaml
```
