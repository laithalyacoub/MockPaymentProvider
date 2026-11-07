namespace MockPaymentProvider.Application.Payments;

public interface IPaymentService
{
    Task<PaymentResponse> ProcessAsync(ProcessPaymentRequest request, CancellationToken cancellationToken);
    Task<PaymentDetailsResponse> GetAsync(string transactionId, CancellationToken cancellationToken);
}
