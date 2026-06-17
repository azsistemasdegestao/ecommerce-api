using Ecommerce.Application.Admin;
using MediatR;

namespace Ecommerce.Application.Admin.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<UserDetailDto>;
