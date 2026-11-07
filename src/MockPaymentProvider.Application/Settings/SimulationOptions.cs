namespace MockPaymentProvider.Application.Settings;

public sealed class SimulationOptions
{
    public const string SectionName = "Simulation";

    public decimal MaxSuccessAmount { get; set; } = 20m;
    public string OutageMobileNumber { get; set; } = "0790000000";
    public string SlowMobileNumber { get; set; } = "0799999999";
    public int SlowDelaySeconds { get; set; } = 10;
}
