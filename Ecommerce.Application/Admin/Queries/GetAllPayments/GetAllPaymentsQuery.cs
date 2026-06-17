using Ecommerce.Application.Common.DTOs;
using MediatR;

namespace Ecommerce.Application.Admin.Queries.GetAllPayments;

public sealed record GetAllPaymentsQuery(int PageNumber, int PageSize) : IRequest<PagedResponse<AdminPaymentSummaryDto>>;
