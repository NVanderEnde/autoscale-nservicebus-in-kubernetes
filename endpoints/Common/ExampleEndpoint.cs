using NServiceBus;

namespace Common;

public static class ExampleEndpoint
{
    public static async Task<IEndpointInstance> CreateAndStartNServiceBusEndpoint(string endpointName, CancellationTokenSource turnMeOff, Action<EndpointConfiguration>? customConfiguration = null)
    {
        var endpointConfiguration = new EndpointConfiguration(endpointName);
        var rabbitMQHostname = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "localhost";
        endpointConfiguration.UseTransport<RabbitMQTransport>()
            .ConnectionString($"host={rabbitMQHostname};username=guest;password=guest")
            .UseConventionalRoutingTopology(QueueType.Quorum);
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.EnableInstallers();
        var metrics = endpointConfiguration.EnableMetrics();
        metrics.SendMetricDataToServiceControl(
            serviceControlMetricsAddress: "Particular.Monitoring",
            interval: TimeSpan.FromSeconds(2)
        );
        customConfiguration?.Invoke(endpointConfiguration);
        var startableEndpoint = await NServiceBus.Endpoint.Create(endpointConfiguration);
        var endpointInstance = await startableEndpoint.Start(turnMeOff.Token);
        return endpointInstance;
    }
}
