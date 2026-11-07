namespace MockPaymentProvider.Application.Auth;

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresInSeconds);
