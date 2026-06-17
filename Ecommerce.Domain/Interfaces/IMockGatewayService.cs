namespace Ecommerce.Domain.Interfaces;

public sealed record GatewayResult(bool Success, string? FailureReason);

public interface IMockGatewayService
{
    Task<GatewayResult> ProcessAsync(Guid paymentId, decimal amount, CancellationToken ct = default);
}
