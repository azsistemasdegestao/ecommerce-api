using MediatR;

namespace Ecommerce.Application.Admin.Commands.AssignRole;

public sealed record AssignRoleCommand(Guid TargetUserId, Guid RequestingAdminId, string Role) : IRequest;
