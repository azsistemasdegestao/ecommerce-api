using Ecommerce.Application.Common.DTOs;

namespace Ecommerce.Application.Orders;

public sealed record OrderSummaryDto(Guid Id, string Status, decimal Total, int ItemCount, DateTime CreatedAt);

public sealed record OrderItemDetailDto(
    Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal);

public sealed record OrderDetailDto(
    Guid Id,
    Guid UserId,
    string Status,
    decimal Total,
    string ShippingAddress,
    IReadOnlyList<OrderItemDetailDto> Items,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public interface IOrderQueryService
{
    Task<PagedResponse<OrderSummaryDto>> GetOrdersAsync(
        Guid userId, int pageNumber, int pageSize, string? status, CancellationToken ct = default);

    Task<OrderDetailDto?> GetByIdAsync(Guid orderId, CancellationToken ct = default);
}
