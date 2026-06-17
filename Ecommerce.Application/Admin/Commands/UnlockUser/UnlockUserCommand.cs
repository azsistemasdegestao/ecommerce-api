using MediatR;

namespace Ecommerce.Application.Admin.Commands.UnlockUser;

public sealed record UnlockUserCommand(Guid TargetUserId) : IRequest;
