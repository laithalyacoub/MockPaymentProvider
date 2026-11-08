# Section 14 files for MockPaymentProvider. Run from C:\dev\zain\MockPaymentProvider
$ErrorActionPreference = "Stop"
if (-not (Test-Path "src\MockPaymentProvider.Api")) { throw "Wrong folder. cd C:\dev\zain\MockPaymentProvider first." }
$utf8 = New-Object System.Text.UTF8Encoding $false
function Write-Code($path, $content) {
  $full = Join-Path (Get-Location) $path
  New-Item -ItemType Directory -Force -Path (Split-Path $full) | Out-Null
  [System.IO.File]::WriteAllText($full, $content.Replace("`r`n","`n").Replace("`n","`r`n") + "`r`n", $utf8)
  Write-Host "created $path"
}

# 1. Packages (Serilog.AspNetCore brings the console sink and the appsettings reader)
dotnet add src\MockPaymentProvider.Api package Serilog.AspNetCore
dotnet add src\MockPaymentProvider.Api package Serilog.Sinks.Seq
if ($LASTEXITCODE -ne 0) { throw "Package install failed. Check your internet connection and run the script again." }

# 2. Files

Write-Code "src/MockPaymentProvider.Api/Middleware/CorrelationIdMiddleware.cs" @'
using Serilog.Context;

namespace MockPaymentProvider.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadOrCreate(context);

        context.Items[HeaderName] = correlationId; // for code that needs it later in this request
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId; // the caller gets it back
            return Task.CompletedTask;
        });

        // Every log line written while this request runs now carries CorrelationId.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static string ReadOrCreate(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        return IsSafe(incoming) ? incoming : Guid.NewGuid().ToString("N");
    }

    // A value from outside goes into our logs, so only short and harmless values are accepted.
    private static bool IsSafe(string value) =>
        value.Length is > 0 and <= 64 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
'@

Write-Code "src/MockPaymentProvider.Api/Program.cs" @'
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MockPaymentProvider.Api.ErrorHandling;
using MockPaymentProvider.Api.Middleware;
using MockPaymentProvider.Application.Auth;
using MockPaymentProvider.Application.Payments;
using MockPaymentProvider.Application.Settings;
using MockPaymentProvider.Infrastructure;
using MockPaymentProvider.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration));

    // ---------- Settings (checked at startup: a missing key stops the app immediately) ----------
    builder.Services
        .AddOptions<ProviderSecurityOptions>()
        .Bind(builder.Configuration.GetSection(ProviderSecurityOptions.SectionName))
        .ValidateDataAnnotations()
        .Validate(o => o.JwtSigningKey.Length >= 32, "Security:JwtSigningKey must be at least 32 characters.")
        .ValidateOnStart();

    builder.Services.Configure<SimulationOptions>(
        builder.Configuration.GetSection(SimulationOptions.SectionName));

    // ---------- Our services ----------
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddScoped<IPaymentService, PaymentService>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    // ---------- Errors: one JSON shape for every failure ----------
    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] =
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();

    // ---------- Bearer token validation ----------
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();

    builder.Services
        .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IOptions<ProviderSecurityOptions>>((jwt, securityOptions) =>
        {
            var security = securityOptions.Value;

            jwt.MapInboundClaims = false; // keep claim names exactly as we wrote them
            jwt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = security.Issuer,
                ValidateAudience = true,
                ValidAudience = security.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(security.JwtSigningKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero // an expired token is expired, with no grace period
            };

            // Answer 401 in the same JSON shape as every other error.
            jwt.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                    var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                    await problems.TryWriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Detail = "A valid bearer token is required. Request a new one from /api/auth/token."
                        }
                    });
                }
            };
        });

    builder.Services.AddAuthorization();

    // ---------- Web API ----------
    builder.Services.AddControllers();
    builder.Services.AddHealthChecks().AddDbContextCheck<ProviderDbContext>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Mock Payment Provider", Version = "v1" });

        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyMiddleware.HeaderName,
            Description = "The provider API key."
        });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the accessToken only, without the word Bearer."
        });

        // Swashbuckle 10 syntax (Microsoft.OpenApi 2.x).
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("ApiKey", document)] = [],
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();   // first, so everything below carries the id
    app.UseSerilogRequestLogging();                  // one summary line per request

    // ---------- The pipeline: the order matters ----------
    app.UseExceptionHandler();   // turns exceptions into JSON errors
    app.UseStatusCodePages();    // gives plain 404 and 405 answers a JSON body too

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<ApiKeyMiddleware>(); // lock 1
    app.UseAuthentication();               // reads the bearer token
    app.UseAuthorization();                // lock 2: [Authorize] is enforced here

    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "The application stopped during startup");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
'@

# appsettings.json: add the Serilog section
$cfg = Join-Path (Get-Location) "src\MockPaymentProvider.Api\appsettings.json"
$j = [System.IO.File]::ReadAllText($cfg)
if (-not $j.Contains('"Serilog"')) {
  $block = @'
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.Seq" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System.Net.Http.HttpClient": "Warning"
      }
    },
    "Enrich": [ "FromLogContext" ],
    "Properties": { "Application": "MockPaymentProvider" },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      },
      { "Name": "Seq", "Args": { "serverUrl": "http://localhost:5341" } }
    ]
  },
'@
  $idx = $j.IndexOf("{")
  $j = $block + $j.Substring($idx + 1)
  [System.IO.File]::WriteAllText($cfg, $j, $utf8)
  Write-Host "patched src/MockPaymentProvider.Api/appsettings.json"
} else { Write-Host "appsettings.json already has Serilog, left alone" }


Write-Host "Done. Now run: dotnet build"