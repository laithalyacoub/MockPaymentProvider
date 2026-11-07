using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MockPaymentProvider.Application.Exceptions;
using MockPaymentProvider.Application.Payments;
using MockPaymentProvider.Domain.Entities;

namespace MockPaymentProvider.Infrastructure.Persistence;

public sealed class PaymentRepository(ProviderDbContext db) : IPaymentRepository
{
    // SQL Server error numbers for "duplicate key".
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public Task<Payment?> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken) =>
        db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TransactionId == transactionId, cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        db.Payments.Add(payment);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            db.Entry(payment).State = EntityState.Detached;
            throw new DuplicateTransactionException(payment.TransactionId);
        }
    }
}
