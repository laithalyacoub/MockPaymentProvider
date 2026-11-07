namespace MockPaymentProvider.Domain.Entities;

public enum PaymentStatus
{
    Success = 1,
    Failed = 2
}

public class Payment
{
    // EF Core fills these two from the database; our code never sets them.
    public int Id { get; private set; }
    public string? PaymentReference { get; private set; }

    public string TransactionId { get; private set; } = null!;
    public string MobileNumber { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string Message { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private Payment() { } // required by EF Core

    public static Payment Create(
        string transactionId,
        string mobileNumber,
        decimal amount,
        PaymentStatus status,
        string message,
        DateTime createdAtUtc)
    {
        return new Payment
        {
            TransactionId = transactionId,
            MobileNumber = mobileNumber,
            Amount = amount,
            Status = status,
            Message = message,
            CreatedAt = createdAtUtc
        };
    }
}