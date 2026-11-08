# Section 15 test files for MockPaymentProvider. Run from C:\dev\zain\MockPaymentProvider
$ErrorActionPreference = "Stop"
if (-not (Test-Path "tests\MockPaymentProvider.Tests")) { throw "Wrong folder. cd C:\dev\zain\MockPaymentProvider first." }
$utf8 = New-Object System.Text.UTF8Encoding $false
function Write-Code($path, $content) {
  $full = Join-Path (Get-Location) $path
  New-Item -ItemType Directory -Force -Path (Split-Path $full) | Out-Null
  [System.IO.File]::WriteAllText($full, $content.Replace("`r`n","`n").Replace("`n","`r`n") + "`r`n", $utf8)
  Write-Host "created $path"
}

# The template test file is not needed
Remove-Item "tests\MockPaymentProvider.Tests\UnitTest1.cs" -ErrorAction SilentlyContinue

Write-Code "tests/MockPaymentProvider.Tests/PaymentServiceTests.cs" @'
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MockPaymentProvider.Application.Exceptions;
using MockPaymentProvider.Application.Payments;
using MockPaymentProvider.Application.Settings;
using MockPaymentProvider.Domain.Entities;

namespace MockPaymentProvider.Tests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

// Behaves like the table: a unique TransactionId, an identity Id, and a reference only for SUCCESS.
internal sealed class FakePaymentRepository : IPaymentRepository
{
    private readonly Dictionary<string, Payment> _byTransaction = new();
    private int _nextId = 1;

    public IReadOnlyCollection<Payment> Saved => _byTransaction.Values;

    public Task<Payment?> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken) =>
        Task.FromResult(_byTransaction.GetValueOrDefault(transactionId));

    public Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (_byTransaction.ContainsKey(payment.TransactionId))
            throw new DuplicateTransactionException(payment.TransactionId);

        var id = _nextId++;
        Set(payment, nameof(Payment.Id), id);
        if (payment.Status == PaymentStatus.Success)
            Set(payment, nameof(Payment.PaymentReference), $"PAY-{50000 + id}");

        _byTransaction[payment.TransactionId] = payment;
        return Task.CompletedTask;
    }

    private static void Set(object target, string property, object value) =>
        typeof(Payment).GetProperty(property)!.SetValue(target, value);
}

public class PaymentServiceTests
{
    private static (PaymentService Service, FakePaymentRepository Repository) Build()
    {
        var repository = new FakePaymentRepository();
        var options = Options.Create(new SimulationOptions { SlowDelaySeconds = 0 }); // do not really wait
        var service = new PaymentService(
            repository, options, new FixedTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<PaymentService>.Instance);
        return (service, repository);
    }

    private static ProcessPaymentRequest Request(string transactionId, string mobile, decimal amount) =>
        new(transactionId, mobile, amount);

    [Fact]
    public async Task An_amount_up_to_twenty_succeeds_with_a_reference()
    {
        var (service, _) = Build();

        var response = await service.ProcessAsync(Request("TXN-1001", "0791234567", 5m), default);

        Assert.Equal("SUCCESS", response.Status);
        Assert.Equal("PAY-50001", response.PaymentReference);
    }

    [Fact]
    public async Task An_amount_above_twenty_fails_without_a_reference()
    {
        var (service, _) = Build();

        var response = await service.ProcessAsync(Request("TXN-1001", "0791234567", 25m), default);

        Assert.Equal("FAILED", response.Status);
        Assert.Null(response.PaymentReference);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_zero_or_negative_amount_is_a_validation_error_and_saves_nothing(double amount)
    {
        var (service, repository) = Build();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.ProcessAsync(Request("TXN-1001", "0791234567", (decimal)amount), default));

        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task The_outage_number_gives_unavailable_and_saves_nothing()
    {
        var (service, repository) = Build();

        await Assert.ThrowsAsync<ProviderUnavailableException>(() =>
            service.ProcessAsync(Request("TXN-1001", "0790000000", 5m), default));

        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task The_slow_number_is_still_processed_normally()
    {
        var (service, repository) = Build();

        var response = await service.ProcessAsync(Request("TXN-1001", "0799999999", 5m), default);

        Assert.Equal("SUCCESS", response.Status);
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task Sending_the_same_request_twice_returns_the_same_result_and_stores_one_row()
    {
        var (service, repository) = Build();

        var first = await service.ProcessAsync(Request("TXN-1001", "0791234567", 5m), default);
        var second = await service.ProcessAsync(Request("TXN-1001", "0791234567", 5m), default);

        Assert.Equal(first, second);
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task Reusing_a_transaction_id_with_different_details_is_a_conflict()
    {
        var (service, _) = Build();
        await service.ProcessAsync(Request("TXN-1001", "0791234567", 5m), default);

        await Assert.ThrowsAsync<PaymentConflictException>(() =>
            service.ProcessAsync(Request("TXN-1001", "0791234567", 6m), default));
    }

    [Fact]
    public async Task Asking_about_an_unknown_payment_is_not_found()
    {
        var (service, _) = Build();

        await Assert.ThrowsAsync<PaymentNotFoundException>(() => service.GetAsync("TXN-404", default));
    }
}
'@

Write-Host "Done. Now run: dotnet test"
