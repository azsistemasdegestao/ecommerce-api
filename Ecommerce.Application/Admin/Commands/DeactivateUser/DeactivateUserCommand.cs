using MediatR;

namespace Ecommerce.Application.Admin.Commands.DeactivateUser;

public sealed record DeactivateUserCommand(Guid TargetUserId, Guid RequestingAdminId) : IRequest;
