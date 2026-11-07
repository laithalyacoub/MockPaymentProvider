using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockPaymentProvider.Application.Exceptions;
using MockPaymentProvider.Application.Settings;

namespace MockPaymentProvider.Application.Auth;

public interface IAuthService
{
    TokenResponse IssueToken(TokenRequest request);
}

public sealed class AuthService(
    IOptions<ProviderSecurityOptions> options,
    ITokenService tokenService,
    ILogger<AuthService> logger) : IAuthService
{
    public TokenResponse IssueToken(TokenRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ClientId))
            errors["clientId"] = ["Client id is required."];
        if (string.IsNullOrWhiteSpace(request.ClientSecret))
            errors["clientSecret"] = ["Client secret is required."];
        if (errors.Count > 0)
            throw new RequestValidationException(errors);

        var security = options.Value;

        // '&' on purpose (not '&&'): both comparisons always run, so timing reveals nothing.
        var credentialsMatch =
            FixedTimeEquals(request.ClientId!, security.ClientId) &
            FixedTimeEquals(request.ClientSecret!, security.ClientSecret);

        if (!credentialsMatch)
        {
            logger.LogWarning("Token request rejected for client {ClientId}", request.ClientId);
            throw new InvalidCredentialsException();
        }

        logger.LogInformation("Token issued to client {ClientId}", request.ClientId);
        return tokenService.CreateToken(request.ClientId!);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
