using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Diagnostics;

/// <summary>
/// Sistem kaynaklarını (Thread, Memory) takip eden akıllı health check.
/// </summary>
public sealed class PerformanceHealthCheck : IHealthCheck
{
    private const long MaxMemoryInBytes = 1024L * 1024L * 1024L; // 1GB limit (örnek)

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        // 1. Thread Starvation Kontrolü
        ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
        ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);

        var isThreadStarvated = workerThreads < minWorkerThreads;

        // 2. Memory Kontrolü
        var allocatedMemory = GC.GetTotalMemory(false);
        var isMemoryLow = allocatedMemory > MaxMemoryInBytes;

        var data = new Dictionary<string, object>
        {
            { "AvailableWorkerThreads", workerThreads },
            { "AvailableCompletionPortThreads", completionPortThreads },
            { "AllocatedMemoryMB", allocatedMemory / 1024 / 1024 },
            { "IsThreadStarvated", isThreadStarvated }
        };

        if (isThreadStarvated || isMemoryLow)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Sistem kaynakları zorlanıyor.", 
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Sistem kaynakları normal.", 
            data: data));
    }
}
