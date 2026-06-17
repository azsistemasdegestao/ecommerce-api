namespace Ecommerce.Application.Admin.Commands.UpdateOrderStatus;

public sealed record UpdateOrderStatusResponse(Guid Id, string Status, DateTime UpdatedAt);
