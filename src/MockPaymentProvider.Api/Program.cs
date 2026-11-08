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
