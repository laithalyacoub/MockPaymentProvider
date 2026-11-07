namespace MockPaymentProvider.Application.Auth;

public interface ITokenService
{
    TokenResponse CreateToken(string clientId);
}
