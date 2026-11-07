namespace MockPaymentProvider.Application.Exceptions;

// becomes 400
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception("The request is not valid.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

// becomes 401
public sealed class InvalidCredentialsException()
    : Exception("The client id or client secret is wrong.");

// becomes 404
public sealed class PaymentNotFoundException(string transactionId)
    : Exception($"Payment '{transactionId}' was not found.");

// becomes 409
public sealed class PaymentConflictException(string transactionId)
    : Exception($"Transaction '{transactionId}' was already processed with different details.");

// becomes 503 (the simulated outage)
public sealed class ProviderUnavailableException()
    : Exception("The payment service is temporarily unavailable.");

// Internal: the database refused a second row with the same TransactionId.
public sealed class DuplicateTransactionException(string transactionId)
    : Exception($"Transaction '{transactionId}' already exists.");
