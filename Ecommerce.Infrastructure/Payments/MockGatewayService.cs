using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Interfaces;

namespace Ecommerce.Infrastructure.Payments;

public sealed class MockGatewayService : IMockGatewayService
{
    private readonly Random _random = new();

    // BR-PAY-004: approval rate and settlement delay vary by simulated payment method.
    public async Task<GatewayResult> ProcessAsync(Guid paymentId, decimal amount, PaymentMethod paymentMethod, CancellationToken ct = default)
    {
        var (minDelayMs, maxDelayMs, approvalRate) = paymentMethod switch
        {
            PaymentMethod.Pix => (100, 300, 0.95),
            PaymentMethod.Boleto => (500, 1500, 0.70),
            _ => (100, 500, 0.80)
        };

        await Task.Delay(_random.Next(minDelayMs, maxDelayMs), ct);

        var success = _random.NextDouble() < approvalRate;
        return success
            ? new GatewayResult(true, null)
            : new GatewayResult(false, "Insufficient funds");
    }
}
