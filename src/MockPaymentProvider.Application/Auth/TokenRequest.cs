namespace MockPaymentProvider.Application.Auth;

public sealed record TokenRequest(string? ClientId, string? ClientSecret);
