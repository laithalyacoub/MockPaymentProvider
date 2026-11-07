using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MockPaymentProvider.Application.Auth;
using MockPaymentProvider.Application.Settings;

namespace MockPaymentProvider.Infrastructure.Security;

public sealed class TokenService(IOptions<ProviderSecurityOptions> options, TimeProvider clock) : ITokenService
{
    public TokenResponse CreateToken(string clientId)
    {
        var security = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(security.JwtSigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = security.Issuer,
            Audience = security.Audience,
            Subject = new ClaimsIdentity([new Claim("sub", clientId), new Claim("client_id", clientId)]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddSeconds(security.TokenLifetimeSeconds),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new TokenResponse(token, "Bearer", security.TokenLifetimeSeconds);
    }
}
