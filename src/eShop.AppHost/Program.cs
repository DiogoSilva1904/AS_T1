using eShop.AppHost;
using Aspire.Hosting;
using eShop.ServiceDefaults;
var builder = DistributedApplication.CreateBuilder(args);

//Is not working
//builder.AddProject<eShop.ServiceDefaults.Extensions>("service-defaults");

//builder.AddForwardedHeaders();

/* builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation() // To instrument HTTP requests
        .AddPrometheusExporter() // To export to Prometheus
    )
    .WithResource(new Resource(new Dictionary<string, object>
    {
        { "service.name", "CatalogApi" },
    }));
 */
//app.UseOpenTelemetryPrometheusScrapingEndpoint();

//builder.AddServiceDefaults();

//builder.Services.AddOpenTelemetryServices();

var redis = builder.AddRedis("redis");
var rabbitMq = builder.AddRabbitMQ("eventbus")
    .WithLifetime(ContainerLifetime.Persistent);

var postgres = builder.AddPostgres("postgres")
    .WithImage("ankane/pgvector")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent);

var otelCollector = builder.AddContainer("otel-collector", "ghcr.io/open-telemetry/opentelemetry-collector-releases/opentelemetry-collector-contrib:0.120.0")
    .WithBindMount("/home/diogo/Documentos/AS/AS_T1/configs/otelcol-config.yml", "/etc/otel/otelcol-config.yml")
    .WithEndpoint(4319, 4317, name: "grpc")  // OTLP gRPC
    .WithEndpoint(4320, 4318, name: "http")  // OTLP HTTP
    .WithEnvironment("ENVOY_PORT", "443")
    .WithEnvironment("HOST_FILESYSTEM", "/hostfs")
    .WithEnvironment("OTEL_COLLECTOR_HOST", "otel-collector")
    .WithEnvironment("OTEL_COLLECTOR_PORT_GRPC", "4317")
    .WithEnvironment("OTEL_COLLECTOR_PORT_HTTP", "4318");

var opensearch = builder.AddContainer("opensearch", "opensearchproject/opensearch:2.19.0")
    .WithEnvironment("cluster.name", "demo-cluster")
    .WithEnvironment("node.name", "demo-node")
    .WithEnvironment("bootstrap.memory_lock", "true")
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms300m -Xmx300m")
    .WithEnvironment("DISABLE_INSTALL_DEMO_CONFIG", "true")
    .WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
    .WithEndpoint(9200, 9200, name: "http", scheme: "http");  // OpenSearch HTTP endpoint


var opensearchDashboards = builder.AddContainer("opensearch-dashboards", "opensearchproject/opensearch-dashboards:2.11.1")
    .WithEnvironment("OPENSEARCH_HOSTS", "http://opensearch:9200")  // Pointing to OpenSearch container
    .WithEndpoint(5601, 5601, name: "http", scheme: "http");  // OpenSearch Dashboards UI (port 5601)

var prometheus = builder.AddContainer("prometheus", "quay.io/prometheus/prometheus:v3.2.0")
    .WithBindMount("/home/diogo/Documentos/AS/AS_T1/configs/prometheus-config.yml","/etc/prometheus/prometheus.yml")
    .WithEndpoint(9090, 9090, name: "http", scheme: "http");  // Prometheus UI

var grafana = builder.AddContainer("grafana", "grafana/grafana:11.5.2")
    .WithEnvironment("GF_INSTALL_PLUGINS", "grafana-opensearch-datasource")
    .WithBindMount("/home/diogo/Documentos/AS/AS_T1/configs/grafana/config/grafana.ini", "/etc/grafana/grafana.ini")
    .WithBindMount("/home/diogo/Documentos/AS/AS_T1/configs/grafana/config/provisioning", "/etc/grafana/provisioning/")
    .WithBindMount("/home/diogo/Documentos/AS/AS_T1/configs/grafana/dashboards", "/etc/grafana/dashboards")
    .WithEndpoint(3000, 3000, name: "http", scheme: "http"); // Grafana UI

var jaeger = builder.AddContainer("jaeger", "jaegertracing/all-in-one:1.66.0")
    .WithEnvironment("COLLECTOR_ZIPKIN_HOST_PORT", ":9411")
    .WithEnvironment("COLLECTOR_OTLP_ENABLED", "true") // Enable OTLP in Jaeger
    .WithEnvironment("METRICS_STORAGE_TYPE", "prometheus") // Store metrics in Prometheus
    .WithEnvironment("PROMETHEUS_SERVER_URL", "http://prometheus:9090")
    .WithEnvironment("PROMETHEUS_QUERY_NORMALIZE_CALLS", "true")
    .WithEnvironment("PROMETHEUS_QUERY_NORMALIZE_DURATION", "true")
    .WithEndpoint(16686, 16686, name: "ui", scheme: "http")  // Jaeger UI
    .WithEndpoint(4317, 4317, name: "grpc")  // OTLP gRPC
    .WithEndpoint(4318, 4318, name: "http"); // OTLP HTTP


var catalogDb = postgres.AddDatabase("catalogdb");
var identityDb = postgres.AddDatabase("identitydb");
var orderDb = postgres.AddDatabase("orderingdb");
var webhooksDb = postgres.AddDatabase("webhooksdb");

var launchProfileName = ShouldUseHttpForEndpoints() ? "http" : "https";

// Services
var identityApi = builder.AddProject<Projects.Identity_API>("identity-api", launchProfileName)
    .WithExternalHttpEndpoints()
    .WithReference(identityDb);

var identityEndpoint = identityApi.GetEndpoint(launchProfileName);

var basketApi = builder.AddProject<Projects.Basket_API>("basket-api")
    .WithReference(redis)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithEnvironment("Identity__Url", identityEndpoint);
redis.WithParentRelationship(basketApi);

var catalogApi = builder.AddProject<Projects.Catalog_API>("catalog-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(catalogDb);

var orderingApi = builder.AddProject<Projects.Ordering_API>("ordering-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(orderDb).WaitFor(orderDb)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("Identity__Url", identityEndpoint);

builder.AddProject<Projects.OrderProcessor>("order-processor")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(orderDb)
    .WaitFor(orderingApi); // wait for the orderingApi to be ready because that contains the EF migrations

builder.AddProject<Projects.PaymentProcessor>("payment-processor")
    .WithReference(rabbitMq).WaitFor(rabbitMq);

var webHooksApi = builder.AddProject<Projects.Webhooks_API>("webhooks-api")
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(webhooksDb)
    .WithEnvironment("Identity__Url", identityEndpoint);

// Reverse proxies
builder.AddProject<Projects.Mobile_Bff_Shopping>("mobile-bff")
    .WithReference(catalogApi)
    .WithReference(orderingApi)
    .WithReference(basketApi)
    .WithReference(identityApi);

// Apps
var webhooksClient = builder.AddProject<Projects.WebhookClient>("webhooksclient", launchProfileName)
    .WithReference(webHooksApi)
    .WithEnvironment("IdentityUrl", identityEndpoint);

var webApp = builder.AddProject<Projects.WebApp>("webapp", launchProfileName)
    .WithExternalHttpEndpoints()
    .WithReference(basketApi)
    .WithReference(catalogApi)
    .WithReference(orderingApi)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithEnvironment("IdentityUrl", identityEndpoint);

// set to true if you want to use OpenAI
bool useOpenAI = false;
if (useOpenAI)
{
    builder.AddOpenAI(catalogApi, webApp);
}

bool useOllama = false;
if (useOllama)
{
    builder.AddOllama(catalogApi, webApp);
}

// Wire up the callback urls (self referencing)
webApp.WithEnvironment("CallBackUrl", webApp.GetEndpoint(launchProfileName));
webhooksClient.WithEnvironment("CallBackUrl", webhooksClient.GetEndpoint(launchProfileName));

// Identity has a reference to all of the apps for callback urls, this is a cyclic reference
identityApi.WithEnvironment("BasketApiClient", basketApi.GetEndpoint("http"))
           .WithEnvironment("OrderingApiClient", orderingApi.GetEndpoint("http"))
           .WithEnvironment("WebhooksApiClient", webHooksApi.GetEndpoint("http"))
           .WithEnvironment("WebhooksWebClient", webhooksClient.GetEndpoint(launchProfileName))
           .WithEnvironment("WebAppClient", webApp.GetEndpoint(launchProfileName));

builder.Build().Run();

// For test use only.
// Looks for an environment variable that forces the use of HTTP for all the endpoints. We
// are doing this for ease of running the Playwright tests in CI.
static bool ShouldUseHttpForEndpoints()
{
    const string EnvVarName = "ESHOP_USE_HTTP_ENDPOINTS";
    var envValue = Environment.GetEnvironmentVariable(EnvVarName);

    // Attempt to parse the environment variable value; return true if it's exactly "1".
    return int.TryParse(envValue, out int result) && result == 1;
}
