namespace Ecommerce.Application.Payments;

public sealed record PaymentDetailDto(
    Guid Id, Guid OrderId, Guid OrderUserId, decimal Amount, string Status, string Provider, string PaymentMethod,
    DateTime CreatedAt, DateTime UpdatedAt);

public interface IPaymentQueryService
{
    Task<PaymentDetailDto?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}
