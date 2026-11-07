using System.ComponentModel.DataAnnotations;

namespace MockPaymentProvider.Application.Settings;

public sealed class ProviderSecurityOptions
{
    public const string SectionName = "Security";

    [Required] public string ApiKey { get; set; } = "";
    [Required] public string ClientId { get; set; } = "";
    [Required] public string ClientSecret { get; set; } = "";
    [Required] public string JwtSigningKey { get; set; } = "";

    public string Issuer { get; set; } = "mock-payment-provider";
    public string Audience { get; set; } = "zain-recharge-api";
    public int TokenLifetimeSeconds { get; set; } = 300;
}
