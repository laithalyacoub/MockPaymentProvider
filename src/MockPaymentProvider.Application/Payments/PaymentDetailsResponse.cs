namespace MockPaymentProvider.Application.Payments;

public sealed record PaymentDetailsResponse(
    string TransactionId,
    string? PaymentReference,
    string MobileNumber,
    decimal Amount,
    string Status,
    string Message,
    DateTime CreatedAt);
