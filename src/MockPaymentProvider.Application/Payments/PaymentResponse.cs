namespace MockPaymentProvider.Application.Payments;

public sealed record PaymentResponse(
    string TransactionId,
    string? PaymentReference,
    string Status,
    string Message);
