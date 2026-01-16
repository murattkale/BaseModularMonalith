using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace BuildingBlocks.Resilience;

/// <summary>
/// Yüksek ölçekli sistemler için merkezi Resilience (Dayanıklılık) politikaları.
/// Polly v8 (ResiliencePipeline) kullanır.
/// </summary>
public static class ResiliencePipelines
{
    public const string DbPipelineId = "db-policy";

    /// <summary>
    /// Veritabanı işlemleri için optimize edilmiş pipeline.
    /// Retry + Circuit Breaker + Timeout.
    /// </summary>
    public static readonly ResiliencePipeline DbPipeline = new ResiliencePipelineBuilder()
        // 1. Retry: Transient (geçici) hatalarda tekrar dene
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<SqlException>().Handle<SocketException>(),
            BackoffType = DelayBackoffType.Exponential, // Üstel geri çekilme
            UseJitter = true, // Jitter (rastgelelik) ekleyerek DB'yi boğma
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(100)
        })
        // 2. Circuit Breaker: Sistem tamamen çökerse denemeyi bırak (fail-fast)
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<SqlException>().Handle<SocketException>(),
            FailureRatio = 0.5, // %50 hata oranında devreyi aç
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15)
        })
        // 3. Timeout: Sorgu çok takılırsa işlemi kes
        .AddTimeout(TimeSpan.FromSeconds(5))
        .Build();
}
