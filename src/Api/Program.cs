using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Endpoints;
using Api.Middlewares;
using Api.RateLimiting;
using BuildingBlocks.Behaviors;
using BuildingBlocks.DomainEvents;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using User.Api;
using User.Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// SERILOG YAPILANDIRMASI (Async sink'ler, structured JSON loglama)
// =============================================================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Async(a => a.Console(new CompactJsonFormatter()))
    .WriteTo.Async(a => a.File(
        new CompactJsonFormatter(),
        "logs/log-.json",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7))
    .CreateLogger();

builder.Host.UseSerilog();

// =============================================================================
// KESTREL YAPILANDIRMASI (Yüksek eşzamanlılık için optimize edilmiş)
// =============================================================================
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    
    // HTTP/1.1 ve HTTP/2 protokollerini aktif et
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
    
    // İstek boyut limitleri ve bağlantı yönetimi
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32KB
    var maxConnections = builder.Configuration.GetValue<int>("Performance:Kestrel:MaxConcurrentConnections", 50000);
    options.Limits.MaxConcurrentConnections = maxConnections;
    options.Limits.MaxConcurrentUpgradedConnections = maxConnections;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

// =============================================================================
// THREADPOOL YAPILANDIRMASI (Donanıma göre optimize edildi)
// =============================================================================
var multiplier = builder.Configuration.GetValue<int>("Performance:MinThreadsMultiplier", 2);
var minThreads = Environment.ProcessorCount * multiplier;
ThreadPool.SetMinThreads(minThreads, minThreads); // Donanım çekirdek sayısına göre dinamik ayar

// =============================================================================
// RESPONSE COMPRESSION (Bant genişliği optimizasyonu)
// =============================================================================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest; // Performans için hız öncelikli
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// =============================================================================
// JSON YAPILANDIRMASI (System.Text.Json - yüksek performans)
// =============================================================================
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// =============================================================================
// JWT KİMLİK DOĞRULAMA (RS256, in-memory doğrulama)
// =============================================================================
var jwtSettings = builder.Configuration.GetSection("Jwt");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            ClockSkew = TimeSpan.Zero, // Clock skew toleransı yok
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };

        // RS256 için RSA public key yükle
        var publicKeyPath = jwtSettings["PublicKeyPath"];
        if (!string.IsNullOrEmpty(publicKeyPath) && File.Exists(publicKeyPath))
        {
            var publicKeyPem = File.ReadAllText(publicKeyPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(rsa);
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Development için symmetric key fallback
            var secretKey = jwtSettings["SecretKey"];
            if (!string.IsNullOrEmpty(secretKey))
            {
                options.TokenValidationParameters.IssuerSigningKey = 
                    new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secretKey));
            }
        }
        else
        {
            throw new InvalidOperationException("Production ortamında RSA Public Key (RS256) zorunludur.");
        }

        // Token'ı sonraki kullanım için sakla
        options.SaveToken = true;
        
        // Auth'da DB çağrısı yok - sadece in-memory doğrulama
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.Headers.Append("X-Token-Expired", "true");
                }
                return Task.CompletedTask;
            }
        };
    });

// =============================================================================
// GÜVENLİK SERVİSLERİ (CORS & RATE LIMITING & ANTIFORGERY)
// =============================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictPolicy", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["https://localhost:3000"])
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("User", policy => policy.RequireRole("User", "Admin"));
});

// =============================================================================
// RATE LIMITING (In-memory, hafif)
// =============================================================================
builder.Services.AddRateLimitingPolicies();

// =============================================================================
// OPENTELEMETRY (Tracing ve Metrikler)
// =============================================================================
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "BaseModularMonolith",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                // Health check'leri trace'den hariç tut
                options.Filter = context => 
                    !context.Request.Path.StartsWithSegments("/health");
            })
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation(options => 
            {
                options.RecordException = true;
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });

// =============================================================================
// DOMAIN EVENT DISPATCHER (Merkezi kayıt)
// =============================================================================
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

// =============================================================================
// MODÜL SERVİSLERİ (DbContext, Repository, ReadService)
// =============================================================================
builder.Services.AddUserModule(builder.Configuration);

// =============================================================================
// MEDIATR VE BEHAVIOR'LAR (Merkezi kayıt - tüm modüller için)
// =============================================================================
builder.Services.AddMediatR(cfg =>
{
    // Tüm modül assembly'lerini kayıt et
    cfg.RegisterServicesFromAssembly(typeof(User.Application.Commands.CreateUserCommand).Assembly);
    
    // Pipeline sırası: Logging -> Validation -> Idempotency -> Transaction -> Audit -> Handler
    // IMPORTANT: Idempotency MUST be BEFORE Transaction so the record is created in same TX
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));
});

// =============================================================================
// VALIDATORS (Merkezi kayıt - tüm modüller için)
// =============================================================================
builder.Services.AddValidatorsFromAssembly(
    typeof(User.Application.Commands.CreateUserCommand).Assembly, 
    ServiceLifetime.Singleton);

// =============================================================================
// HEALTH CHECKS (İleri Seviye İzleme)
// =============================================================================
builder.Services.AddHealthChecks()
    .AddCheck<Api.Diagnostics.PerformanceHealthCheck>("Performance")
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "SQL Server",
        tags: ["db", "sql"]);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = Api.Serialization.AppJsonContext.Default;
});

// =============================================================================
// OPENAPI / SWAGGER YAPILANDIRMASI (Swashbuckle)
// =============================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =============================================================================
// UYGULAMA OLUŞTUR
// =============================================================================
var app = builder.Build();

// =============================================================================
// MIDDLEWARE PIPELINE
// =============================================================================
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Swagger UI (Geliştirme ortamında aktif)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "High Performance API v1");
        options.RoutePrefix = "swagger"; // /swagger adresinde erişilebilir
        options.DocumentTitle = "High Performance API - Swagger UI";
    });
}

// Security Headers (Must be really early, before other middlewares might output something)
app.UseMiddleware<Api.Middlewares.SecurityHeadersMiddleware>();

        // =============================================================================
        // GÜVENLİK VE PERFORMANS PIPELINE
        // =============================================================================
        
        // Güvenlik ve Performans Hattı
        app.UseCors("StrictPolicy");
        app.UseRateLimiter(); 
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery(); // Antiforgery middleware eklendi
        
        app.UseMiddleware<SecurityAuditMiddleware>();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseMiddleware<ServerTimingMiddleware>();

        // Response compression
        app.UseResponseCompression();

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
            };
            
            // Health check endpoint'lerini loglama
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (httpContext.Request.Path.StartsWithSegments("/health"))
                    return Serilog.Events.LogEventLevel.Verbose;
                
                return elapsed > 500 
                    ? Serilog.Events.LogEventLevel.Warning 
                    : Serilog.Events.LogEventLevel.Information;
            };
        });

        // app.UseAuthentication(); -- Üst tarafa taşındı
        // app.UseAuthorization(); -- Üst tarafa taşındı

// Prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint();

// =============================================================================
// ENDPOINT MAPPING
// =============================================================================
app.MapHealthEndpoints();
app.MapUserModule();

// =============================================================================
// VERİTABANI OLUŞTURMA (Sadece Geliştirme/Test için)
// =============================================================================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<User.Infrastructure.Persistence.UserDbContext>();
    // context.Database.EnsureCreated(); // Migration yoksa bunu kullanırız
    await context.Database.MigrateAsync();
}

// =============================================================================
// UYGULAMAYI ÇALIŞTIR
// =============================================================================
try
{
    Log.Information("Uygulama başlatılıyor...");
    // Extreme performance: SQL Server tuning (Sadece Development'ta aktif)
    if (app.Environment.IsDevelopment())
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<User.Infrastructure.Persistence.UserDbContext>();
            await context.Database.ExecuteSqlRawAsync("IF EXISTS (SELECT 1 FROM sys.databases WHERE name = 'HighPerfDb') ALTER DATABASE HighPerfDb SET DELAYED_DURABILITY = FORCED;");
        }
        catch { /* Ignore if not supported or already set */ }
    }

app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Uygulama beklenmedik şekilde sonlandı");
}
finally
{
    Log.CloseAndFlush();
}
