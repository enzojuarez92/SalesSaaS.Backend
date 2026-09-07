using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SalesSaaS.Infrastructure.Health;

public sealed class AfipHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Afip");
            using var response = await client.GetAsync("https://wswhomo.afip.gov.ar/wsfev1/service.asmx?WSDL", cancellationToken);
            return (int)response.StatusCode < 500 ? HealthCheckResult.Healthy("AFIP homologation endpoint is reachable.") : HealthCheckResult.Degraded("AFIP endpoint returned a server error.");
        }
        catch (Exception exception) { return HealthCheckResult.Degraded("AFIP endpoint is unavailable.", exception); }
    }
}
