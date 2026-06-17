namespace Ecommerce.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderResponse(
    Guid Id, string Status, decimal Total, string ShippingAddress, int ItemCount, DateTime CreatedAt);
