using Microsoft.Extensions.Diagnostics.HealthChecks;
using RdtClient.Service.BackgroundServices;

public class StartupHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Startup.Ready
                                   ? HealthCheckResult.Healthy()
                                   : HealthCheckResult.Unhealthy("Startup tasks are still running."));
    }
}
