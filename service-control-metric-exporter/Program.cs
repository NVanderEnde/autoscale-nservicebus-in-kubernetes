using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

HttpClient httpClient = new ();
// set in the Kubernetes manifest at ../kubernetes/service-control-metric-exporter.yml
var monitoringApiHost = Environment.GetEnvironmentVariable("MONITORING_API_HOST");
JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

HttpRequestMessage CreateRequest(string endpoint)
{
    // this is an unsupported use of the ServiceControl API, and may break in future versions
    var uri = $"http://{monitoringApiHost}/monitored-endpoints/{endpoint}?history=1";
    HttpRequestMessage request = new (HttpMethod.Get, uri);
    return request;
}

async Task<ServiceControlEndpointData> GetEndpointData(string endpoint)
{
    var endpointResponse = await httpClient.SendAsync(CreateRequest(endpoint));
    var endpointData = await endpointResponse.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<ServiceControlEndpointData>(endpointData ?? string.Empty, jsonOptions) ?? throw new Exception("Failed to deserialize response");
}

string FormatMetricForPrometheus(string metricName, float quantity) => $"{metricName} {quantity}";

// the endpoint names are .ToLower()'d because upper or mixed case external metric names are a no-no
// ref: https://github.com/kubernetes/kubernetes/issues/72996
app.MapGet("/{endpoint}/queue-depth", async (string endpoint) =>
{
    return FormatMetricForPrometheus($"{endpoint.ToLower()}_queue_depth", (await GetEndpointData(endpoint)).Digest.Metrics.QueueLength.Latest);
});

app.MapGet("/{endpoint}/critical-time", async (string endpoint) =>
{
    // report on the average critical time for the endpoint, because that gives us a better idea of how the endpoint is performing than the latest value
    return FormatMetricForPrometheus($"{endpoint.ToLower()}_critical_time", (await GetEndpointData(endpoint)).Digest.Metrics.CriticalTime.Average);
});

app.Run();

public record ProcessingTime(float Latest, float Average);

public record CriticalTime(float Latest, float Average);

public record Retries(float Latest, float Average);

public record Throughput(float Latest, float Average);

public record QueueLength(float Latest, float Average);

public record Metrics
(
    ProcessingTime ProcessingTime,
    CriticalTime CriticalTime,
    Retries Retries,
    Throughput Throughput,
    QueueLength QueueLength
);

public record Digest(Metrics Metrics);

public record ServiceControlEndpointData
(
    Digest Digest,
    List<object> Instances,
    List<object> MessageTypes,
    Dictionary<string, object> MetricDetails
);