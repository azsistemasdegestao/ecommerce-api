namespace Ecommerce.Application.Orders.Commands.CancelOrder;

public sealed record CancelOrderResponse(Guid Id, string Status, DateTime UpdatedAt);
