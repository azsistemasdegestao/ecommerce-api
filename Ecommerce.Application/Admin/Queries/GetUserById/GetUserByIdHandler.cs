using Ecommerce.Application.Admin;
using Ecommerce.Application.Common.Exceptions;
using MediatR;

namespace Ecommerce.Application.Admin.Queries.GetUserById;

public sealed class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, UserDetailDto>
{
    private readonly IAdminQueryService _adminQueryService;

    public GetUserByIdHandler(IAdminQueryService adminQueryService)
    {
        _adminQueryService = adminQueryService;
    }

    public async Task<UserDetailDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        return await _adminQueryService.GetUserByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");
    }
}
