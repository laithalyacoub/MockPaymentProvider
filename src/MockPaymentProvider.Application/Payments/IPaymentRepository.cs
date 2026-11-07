using MockPaymentProvider.Domain.Entities;

namespace MockPaymentProvider.Application.Payments;

public interface IPaymentRepository
{
    Task<Payment?> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken);

    /// <summary>Saves the payment. Throws DuplicateTransactionException if the id already exists.</summary>
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}
