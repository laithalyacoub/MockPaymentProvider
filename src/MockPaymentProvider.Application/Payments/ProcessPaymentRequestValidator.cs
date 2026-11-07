using System.Text.RegularExpressions;
using MockPaymentProvider.Application.Exceptions;

namespace MockPaymentProvider.Application.Payments;

public static class ProcessPaymentRequestValidator
{
    // [0-9] on purpose: \d would also accept Arabic-Indic digits.
    private static readonly Regex MobilePattern = new(@"^07[789][0-9]{7}$", RegexOptions.Compiled);

    public static void ValidateOrThrow(ProcessPaymentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TransactionId))
            errors["transactionId"] = ["Transaction id is required."];
        else if (request.TransactionId.Trim().Length > 50)
            errors["transactionId"] = ["Transaction id must be 50 characters or fewer."];

        if (string.IsNullOrWhiteSpace(request.MobileNumber))
            errors["mobileNumber"] = ["Mobile number is required."];
        else if (!MobilePattern.IsMatch(request.MobileNumber))
            errors["mobileNumber"] = ["Mobile number must be 10 digits starting with 077, 078 or 079."];

        if (request.Amount is null)
            errors["amount"] = ["Amount is required."];
        else if (request.Amount <= 0)
            errors["amount"] = ["Amount must be greater than 0."];
        else if (decimal.Round(request.Amount.Value, 2) != request.Amount.Value)
            errors["amount"] = ["Amount can have at most 2 decimal places."];

        if (errors.Count > 0)
            throw new RequestValidationException(errors);
    }
}
