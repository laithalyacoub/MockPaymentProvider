using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;
using MockPaymentProvider.Application.Exceptions;

namespace MockPaymentProvider.Api.ErrorHandling;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // The caller hung up. Nobody is listening, so there is nothing to answer.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        (int Status, string Title, IDictionary<string, string[]>? Errors) result = exception switch
        {
            RequestValidationException e => (StatusCodes.Status400BadRequest, "One or more validation errors occurred.", e.Errors),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Invalid credentials.", null),
            PaymentNotFoundException => (StatusCodes.Status404NotFound, "Payment not found.", null),
            PaymentConflictException => (StatusCodes.Status409Conflict, "Transaction id already used with different details.", null),
            ProviderUnavailableException => (StatusCodes.Status503ServiceUnavailable, "The payment service is temporarily unavailable.", null),
            SqlException or RetryLimitExceededException => (StatusCodes.Status503ServiceUnavailable, "The database is temporarily unavailable.", null),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null)
        };

        if (result.Status == StatusCodes.Status500InternalServerError || exception is SqlException or RetryLimitExceededException)
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path.Value);
        else
            logger.LogWarning("Request ended with {StatusCode}: {Reason}", result.Status, exception.Message);

        var problem = result.Errors is null
            ? new ProblemDetails { Status = result.Status, Title = result.Title }
            : new ValidationProblemDetails(result.Errors) { Status = result.Status, Title = result.Title };

        // For a 500 or a database failure the detail stays generic. The real message is in Seq, not in the response.
        problem.Detail = exception is RequestValidationException or InvalidCredentialsException or PaymentNotFoundException
            or PaymentConflictException or ProviderUnavailableException
            ? exception.Message
            : "Something went wrong. Quote the traceId when you contact support.";

        httpContext.Response.StatusCode = result.Status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}
