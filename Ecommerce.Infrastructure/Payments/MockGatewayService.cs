using Ecommerce.Domain.Interfaces;

namespace Ecommerce.Infrastructure.Payments;

public sealed class MockGatewayService : IMockGatewayService
{
    private readonly Random _random = new();

    // BR-PAY-004: 80% approval, 20% failure, 100-500ms delay
    public async Task<GatewayResult> ProcessAsync(Guid paymentId, decimal amount, CancellationToken ct = default)
    {
        var delay = _random.Next(100, 500);
        await Task.Delay(delay, ct);

        var success = _random.NextDouble() > 0.20;
        return success
            ? new GatewayResult(true, null)
            : new GatewayResult(false, "Insufficient funds");
    }
}
