# Concepts

Deploying message processing endpoints as container is attractive. Scaling containerized deployments benefits from using container orchestration technology like Kubernetes. Kubernetes has built-in support for [horizontal pod auto-scaling](https://kubernetes.io/docs/tasks/run-application/horizontal-pod-autoscale/) (HPA). NServiceBus endpoints can [surface information](https://docs.particular.net/monitoring/metrics/) that can be used by HPAs to scale out when necessary. The [SerivceControl monitoring](https://docs.particular.net/servicecontrol/monitoring-instances/) server provides a central API that (unofficially) can be used for simple metrics like queue depth and critical time across all endpoints. These metrics can then be connected to a HPA by means of an [external metric](https://kubernetes.io/docs/reference/external-api/external-metrics.v1beta1/) in Kubernetes. These NServiceBus metrics are connected to [Prometheus](https://prometheus.io/docs/introduction/overview/) by a simple metrics exporter process. These Prometheus metrics are connected to the external metric API in Kubernetes by way of the [Prometheus Adapter](https://github.com/kubernetes-sigs/prometheus-adapter/tree/master). 

NServiceBus Endpoint -> Service Control -> Metrics Exporter -> Prometheus -> Prometheus Adapter - External Metrics API -> Horizontal Pod Autoscaler

Each of the moving parts contained in this repository has been annotated with in-line commentary to explain what it is and how it works. Included in this top-level README is a high-level guide to installation and experimentation.

Note that although in this example everything is running in Kubernetes (and in the same cluster!), this isn't necessary. The idea here is to have a self-contained end-to-end example, but even in learning/experimentation you might have (for instance) the message bus/transport or SerivceControl elsewhere. The only things that *need* to be in the cluster are Prometheus, the Prometheus Adapter, the endpoints, and the HPA.

# Pre-requisite tools

You will need:

- A Kubernetes cluster. I used [minikube](https://minikube.sigs.k8s.io/docs/) but there's no reason that you couldn't use [kind](https://kind.sigs.k8s.io/) or a real cluster - anything you can talk to with `kubectl` and mess around with. This README assumes you are using minikube and that `kubectl` is short-hand for `minikube kubectl --`
- [Helm](https://helm.sh/) for simplicity of deployment for Prometheus. You could elide Helm and manage that another way if you like (or if your cluster already has this installed)
- [Docker](https://www.docker.com/)
- [.NET](https://dotnet.microsoft.com/en-us/download/dotnet)

# Installation

Some of the Kubernetes deployments are regular manfiests and some are Helm charts. We build Docker images for the NServiceBus endpoints and the metrics exporter.

## RabbitMQ

We use RabbitMQ as the message transport.

```
kubectl apply -f kubernetes/rabbit-mq.yml
```

## ServiceControl

Largely taken from Particular's [PlatformContainerExamples](https://github.com/Particular/PlatformContainerExamples/tree/main/kubernetes) repository. Not all of these are necessary and all that's required is the monitoring deployment. But it can be useful to have e.g. ServicePulse available to quickly confirm that monitoring is working as expected and it affirms that things work with a full deployment of the ServiceControl suite.

```
kubectl apply -f kubernetes/servicecontrol-monitoring.deployment.yaml
kubectl apply -f kubernetes/servicecontrol-audit.statefulset.yaml
kubectl apply -f kubernetes/servicecontrol-error.statefulset.yaml
kubectl apply -f kubernetes/servicepulse.deployment.yaml
kubectl apply -f kubernetes/ingress.yml
```

## NServiceBus Endpoints

Now that our message transport is up and running, let's stand up a couple of NServiceBus endpoints. First, we need to build the Docker images.

```
cd endpoints
docker build --target runtime-alpha -t alpha-app:latest .
docker build --target runtime-beta -t beta-app:latest .
```

Once these have been built, we need to load the images into minikube's image repository.

```
minikube image load alpha-app:latest
minikube image load beta-app:latest
```

Then, we can apply the manifests.

```
kubectl apply -f kubernetes/alpha.yml
kubectl apply -f kubernetes/beta.yml
```

Confirm that they are running properly by inspecting the pods and pod logs.
```
kubectl get pods

NAME                                                              READY   STATUS    RESTARTS   AGE
alpha-endpoint-55c75bd79c-7lcgz                                   1/1     Running   0          2s
beta-endpoint-55666d78fc-m5mfl                                    1/1     Running   0          1s

kubectl logs alpha-endpoint-55c75bd79c-7lcgz
Hello, World!
[...NServiceBus Endpoint Logging...]
```

## Metrics Exporter

Now that ServiceControl is up and running, we need to have a metrics exporter that will produce Prometheus-compatible metrics information. This has been implemented as a .NET 9 minimal API, but anything that can make HTTP requests of the ServiceControl API respond to HTTP requests made by a Prometheus scrape job will work.

```
cd service-control-metric-exporter
docker build -t service-control-metric-exporter:latest .
```

As with the NServiceBus endpoints, we need to load the resulting image into minikube and then apply the manifest.

```
minikube image load service-control-metric-exporter:latest
kubectl apply -f kubernetes/service-control-metrics-exporter.yml
```

You can reach into the cluster and make cURL requests of the metric exporter endpoints to confirm that it's working as expected.
```
GET
/Alpha/queue-depth

alpha-queue-depth 0
```

## Prometheus and the Prometheus Adapter

We're availing ourselves of the community-maintained Helm charts for Prometheus and its associated tooling: [Prometheus Community Helm Charts](https://github.com/prometheus-community/helm-charts)

Once you've added that chart repository, you can install these charts referencing the configuration files included in this repository:

```
helm install prometheus -f kubernetes/helm/prometheus/values.yml prometheus-community/prometheus

helm install prometheus-adapter -f kubernetes/helm/prometheus-adapter/values.yml prometheus-community/prometheus-adapter 
```

The `values.yml` configuration for Prometheus includes scrape jobs for both endpoints for the metrics exported from ServiceControl.

```
    - job_name: 'service-control-queue-depth-alpha'
    scrape_interval: 5s
    metrics_path: /Alpha/queue-depth
    static_configs:
        - targets: 
        - 'service-control-metric-exporter:33699'
        labels:
            group: 'nservicebus-endpoint-data'
[...etc...]
```

The `values.yml` configuration for the Prometheus Adapter includes external rules for these metrics that use the group label and metric name.

```
seriesQuery: '{group="nservicebus-endpoint-data",__name__=~".*queue_depth"}'
```

The queue depth is treated as a simple number. The critical time is a series rate over a five minute span. These are just example metrics and you may want to produce different ones.

You can confirm that everything is working by viewing the metrics in Prometheus, and accessing the external metrics API in the cluster.

```
kubectl get --raw "/apis/external.metrics.k8s.io/v1beta1/"
{
    "kind": "APIResourceList",
    "apiVersion": "v1",
    "groupVersion": "external.metrics.k8s.io/v1beta1",
    "resources": [
        {
            "name": "beta_critical_time",
            "singularName": "",
            "namespaced": true,
            "kind": "ExternalMetricValueList",
            "verbs": [
                "get"
            ]
        },
        {
            "name": "alpha_queue_depth",
            "singularName": "",
            "namespaced": true,
            "kind": "ExternalMetricValueList",
            "verbs": [
                "get"
            ]
        },
        {
            "name": "beta_queue_depth",
            "singularName": "",
            "namespaced": true,
            "kind": "ExternalMetricValueList",
            "verbs": [
                "get"
            ]
        },
        {
            "name": "alpha_critical_time",
            "singularName": "",
            "namespaced": true,
            "kind": "ExternalMetricValueList",
            "verbs": [
                "get"
            ]
        }
    ]
}

kc get --raw "/apis/external.metrics.k8s.io/v1beta1/namespaces/default/alpha_queue_depth"
{
    "kind": "ExternalMetricValueList",
    "apiVersion": "external.metrics.k8s.io/v1beta1",
    "metadata": {},
    "items": [
        {
            "metricName": "alpha_queue_depth",
            "metricLabels": {
                "__name__": "alpha_queue_depth",
                "group": "nservicebus-endpoint-data",
                "instance": "service-control-metric-exporter:33699",
                "job": "service-control-queue-depth-alpha"
            },
            "timestamp": "[...]]",
            "value": "0"
        }
    ]
}
```

# Running a Test

Put some messages in the endpoint queues and you'll be able to observe the HPA detect the queue depth and scale out accordingly.

```
kubectl describe hpa alpha

Name:                                          alpha
Namespace:                                     default
Labels:                                        <none>
Annotations:                                   <none>
CreationTimestamp:                             [...]
Reference:                                     Deployment/alpha-endpoint
Metrics:                                       ( current / target )
  "alpha_queue_depth" (target average value):  994 / 100
Min replicas:                                  1
Max replicas:                                  10
Deployment pods:                               1 current / 10 desired
Conditions:
  Type            Status  Reason            Message
  ----            ------  ------            -------
  AbleToScale     True    SucceededRescale  the HPA controller was able to update the target scale to 4
  ScalingActive   True    ValidMetricFound  the HPA was able to successfully calculate a replica count from external metric alpha_queue_depth(nil)
  ScalingLimited  True    ScaleUpLimit      the desired replica count is increasing faster than the maximum scale rate
Events:
  Type    Reason             Age                From                       Message
  ----    ------             ----               ----                       -------
  Normal  SuccessfulRescale  1s (x2 over 6h2m)  horizontal-pod-autoscaler  New size: 4; reason: external metric alpha_queue_depth(nil) above target
```

A .NET [console application](/endpoints/Producer/) is included in this repository. You can connect this to the RabbitMQ instance (with e.g. `minikube service rabbitmq --url`) running in the cluster, or whatever message bus you're using, and publish events to which the two demonstration endpoints are subscribed.

```
cd endpoints/Producer
# what you do next depends on your shell - but you'll want to set the RABBITMQ_HOSTNAME environment variable to point at your RabbitMQ instance.
# Windows cmd
set RABBITMQ_HOSTNAME=192.168.49.2:30072
# PowerShell
$Env:RABBITMQ_HOSTNAME=192.168.49.2:30072
# Bash/etc.
export RABBITMQ_HOSTNAME=192.168.49.2:30072

# Once set,
dotnet run
> 1000 # or whatever
```

# Extension

The horizontal pod autoscalers in this repository are only paying attention to their endpoints' respective queue depth. This is a simple metric to scale off of, because it is reasonable that you will want to empty the queue and will want to scale horizontally as the queue depth increases. Other important metrics, that are useful for making more intelligent scaling decisions, are the processing and critical times of messages. A critical time that is stable and within the SLAs of the endpoints' business responsibilities indicates that the scale is adequate. A delta between the processing and critical times indicates that the endpoint is struggling with demand, even though throughput might be stable and satisfactory. Processing times could be cross-referenced with queue depth to predict critical time and compare the prediction against the endpoint SLAs to understand if scaling is warranted. The sample size of these metrics should be considered with the aim of avoiding abrupt or spikey scaling behavior. The rate of change of these metrics should be considered to better anticipate scaling needs. Expressing these kinds of decision-making as Prometheus metrics and adapter rules requires a nuanced understanding of the system being scaled. And care must be taken to ensure that sane resource limits are imposed on Kubernetes deployments, and that scaling out endpoints does not exacerbate bottleneck issues in dependent resources (such as un-scaled HTTP APIs or databases).

An interesting design that could be implemented using these techniques is a multi-tenant NServiceBus deployment - with endpoint instances per tenant - that has a zero minimum replica count. Idle NServiceBus endpoints (that are just polling empty queues) are not expensive and so the optimization from 1 -> 0 as a minimum replica count is not virtuous for typical deployments of ~20 endpoints. However, the aggregate system of N tenants - 20(N) 'idle' endpoint pods - may represent non-trivial resource utilization at idle. The ability to schedule pods in response to the arrival of messages in a previously empty queue is attractive for multi-tenant systems that have inconsistent tenant activity, to avoid consuming resources when there is no work to be done.