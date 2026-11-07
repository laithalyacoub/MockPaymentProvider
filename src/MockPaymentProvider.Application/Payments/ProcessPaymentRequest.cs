namespace MockPaymentProvider.Application.Payments;

public sealed record ProcessPaymentRequest(
    string? TransactionId,
    string? MobileNumber,
    decimal? Amount);
