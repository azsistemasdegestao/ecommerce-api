using Ecommerce.Domain.Enums;

namespace Ecommerce.Domain.Interfaces;

public sealed record GatewayResult(bool Success, string? FailureReason);

public interface IMockGatewayService
{
    Task<GatewayResult> ProcessAsync(Guid paymentId, decimal amount, PaymentMethod paymentMethod, CancellationToken ct = default);
}
