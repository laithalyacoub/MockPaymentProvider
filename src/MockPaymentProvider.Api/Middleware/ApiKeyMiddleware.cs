using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MockPaymentProvider.Application.Settings;

namespace MockPaymentProvider.Api.Middleware;

public sealed class ApiKeyMiddleware(
    RequestDelegate next,
    IOptions<ProviderSecurityOptions> options,
    IProblemDetailsService problemDetails,
    ILogger<ApiKeyMiddleware> logger)
{
    public const string HeaderName = "X-Api-Key";

    // Health checks and the Swagger page do not need the key.
    private static readonly string[] OpenPaths = ["/health", "/swagger"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (OpenPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();
        if (!IsValid(provided))
        {
            // Log that it happened, never the value that was sent.
            logger.LogWarning("Rejected request to {Path}: missing or invalid API key", context.Request.Path.Value);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "A valid X-Api-Key header is required."
                }
            });
            return;
        }

        await next(context);
    }

    private bool IsValid(string provided)
    {
        if (string.IsNullOrEmpty(provided)) return false;

        var expected = Encoding.UTF8.GetBytes(options.Value.ApiKey);
        var actual = Encoding.UTF8.GetBytes(provided);

        // Compares in constant time, so response timing cannot be used to guess the key.
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
