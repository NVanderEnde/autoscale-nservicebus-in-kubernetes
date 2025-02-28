namespace Common;

public static class ExampleEndpoint
{
    public static async Task<IEndpointInstance> CreateAndStartNServiceBusEndpoint(string endpointName, CancellationTokenSource turnMeOff, bool sendOnly = false)
    {
        EndpointConfiguration endpointConfiguration = new(endpointName);
        var rabbitMQHostname = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "localhost";
        endpointConfiguration.UseTransport<RabbitMQTransport>()
            .ConnectionString($"host={rabbitMQHostname};username=guest;password=guest")
            .UseConventionalRoutingTopology(QueueType.Quorum);
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.EnableInstallers();
        if (sendOnly)
        {
            endpointConfiguration.SendOnly();
        }
        else
        {
            var metrics = endpointConfiguration.EnableMetrics();
            metrics.SendMetricDataToServiceControl(
                serviceControlMetricsAddress: "Particular.Monitoring",
                interval: TimeSpan.FromSeconds(2)
            );
        }
        var startableEndpoint = await Endpoint.Create(endpointConfiguration);
        var endpointInstance = await startableEndpoint.Start(turnMeOff.Token);
        return endpointInstance;
    }
}
