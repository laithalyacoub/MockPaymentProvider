using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MockPaymentProvider.Application.Payments;

namespace MockPaymentProvider.Api.Controllers;

[ApiController]
[Authorize] // every action here needs a valid bearer token
[Route("api/payments")]
public class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    /// <summary>Process a payment. Sending the same transactionId again returns the stored result.</summary>
    [HttpPost("process")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PaymentResponse>> Process(
        [FromBody] ProcessPaymentRequest request, CancellationToken cancellationToken) =>
        Ok(await paymentService.ProcessAsync(request, cancellationToken));

    /// <summary>Ask what happened to a payment (status inquiry).</summary>
    [HttpGet("{transactionId}")]
    [ProducesResponseType<PaymentDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentDetailsResponse>> Get(
        string transactionId, CancellationToken cancellationToken) =>
        Ok(await paymentService.GetAsync(transactionId, cancellationToken));
}
