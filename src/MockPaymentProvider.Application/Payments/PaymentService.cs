using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockPaymentProvider.Application.Common;
using MockPaymentProvider.Application.Exceptions;
using MockPaymentProvider.Application.Settings;
using MockPaymentProvider.Domain.Entities;

namespace MockPaymentProvider.Application.Payments;

public sealed class PaymentService(
    IPaymentRepository repository,
    IOptions<SimulationOptions> simulationOptions,
    TimeProvider clock,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentResponse> ProcessAsync(
        ProcessPaymentRequest request, CancellationToken cancellationToken)
    {
        // 1. Bad input never reaches the database. (400)
        ProcessPaymentRequestValidator.ValidateOrThrow(request);

        var transactionId = request.TransactionId!.Trim();
        var mobileNumber = request.MobileNumber!;
        var amount = request.Amount!.Value;
        var simulation = simulationOptions.Value;

        logger.LogInformation(
            "Processing payment {TransactionId} for {MobileNumber}, amount {Amount}",
            transactionId, MobileMasking.Mask(mobileNumber), amount);

        // 2. Simulated outage: answer 503 and save nothing. (503)
        if (mobileNumber == simulation.OutageMobileNumber)
        {
            logger.LogWarning("Simulated outage for payment {TransactionId}", transactionId);
            throw new ProviderUnavailableException();
        }

        // 3. Same transaction id again: return the stored answer, or refuse if the details differ.
        var existing = await repository.FindByTransactionIdAsync(transactionId, cancellationToken);
        if (existing is not null)
            return Replay(existing, mobileNumber, amount);

        // 4. Simulated slow provider.
        if (mobileNumber == simulation.SlowMobileNumber)
        {
            logger.LogWarning(
                "Simulated slow processing ({Seconds} s) for payment {TransactionId}",
                simulation.SlowDelaySeconds, transactionId);

            // Not cancellable on purpose. A real provider keeps working after the caller
            // gives up, and that is exactly how a payment ends up UNKNOWN on the Zain side.
            await Task.Delay(TimeSpan.FromSeconds(simulation.SlowDelaySeconds), CancellationToken.None);
        }

        // 5. Decide the result.
        var (status, message) = amount <= simulation.MaxSuccessAmount
            ? (PaymentStatus.Success, "Payment completed successfully")
            : (PaymentStatus.Failed, $"Amount exceeds the allowed limit of {simulation.MaxSuccessAmount:0} JOD");

        var payment = Payment.Create(
            transactionId, mobileNumber, amount, status, message, clock.GetUtcNow().UtcDateTime);

        // 6. Save. Once we have decided, we finish the job even if the caller has left.
        try
        {
            await repository.AddAsync(payment, CancellationToken.None);
        }
        catch (DuplicateTransactionException)
        {
            // Two requests with the same id arrived together and the other one won the race.
            var winner = await repository.FindByTransactionIdAsync(transactionId, CancellationToken.None);
            if (winner is null) throw;
            return Replay(winner, mobileNumber, amount);
        }

        logger.LogInformation(
            "Payment {TransactionId} finished as {Status} with reference {PaymentReference}",
            transactionId, ToWire(payment.Status), payment.PaymentReference);

        return ToResponse(payment);
    }

    public async Task<PaymentDetailsResponse> GetAsync(
        string transactionId, CancellationToken cancellationToken)
    {
        var payment = await repository.FindByTransactionIdAsync(transactionId.Trim(), cancellationToken)
            ?? throw new PaymentNotFoundException(transactionId);

        return new PaymentDetailsResponse(
            payment.TransactionId,
            payment.PaymentReference,
            payment.MobileNumber,
            payment.Amount,
            ToWire(payment.Status),
            payment.Message,
            payment.CreatedAt);
    }

    private PaymentResponse Replay(Payment existing, string mobileNumber, decimal amount)
    {
        if (existing.MobileNumber != mobileNumber || existing.Amount != amount)
        {
            logger.LogWarning(
                "Transaction {TransactionId} was sent again with different details", existing.TransactionId);
            throw new PaymentConflictException(existing.TransactionId);
        }

        logger.LogInformation(
            "Transaction {TransactionId} was sent again; returning the stored result", existing.TransactionId);
        return ToResponse(existing);
    }

    private static PaymentResponse ToResponse(Payment payment) =>
        new(payment.TransactionId, payment.PaymentReference, ToWire(payment.Status), payment.Message);

    private static string ToWire(PaymentStatus status) => status.ToString().ToUpperInvariant();
}
