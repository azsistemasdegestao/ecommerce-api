using MediatR;

namespace Ecommerce.Application.Admin.Commands.UpdateOrderStatus;

public sealed record UpdateOrderStatusCommand(Guid OrderId, string Status) : IRequest<UpdateOrderStatusResponse>;
