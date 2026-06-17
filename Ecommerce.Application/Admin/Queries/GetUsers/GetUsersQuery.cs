using Ecommerce.Application.Admin;
using Ecommerce.Application.Common.DTOs;
using MediatR;

namespace Ecommerce.Application.Admin.Queries.GetUsers;

public sealed record GetUsersQuery(int PageNumber, int PageSize, string? Search)
    : IRequest<PagedResponse<UserSummaryDto>>;
