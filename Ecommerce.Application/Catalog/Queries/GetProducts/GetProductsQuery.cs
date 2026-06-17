using Ecommerce.Application.Catalog;
using Ecommerce.Application.Common.DTOs;
using MediatR;

namespace Ecommerce.Application.Catalog.Queries.GetProducts;

public sealed record GetProductsQuery(
    int PageNumber,
    int PageSize,
    string? CategorySlug,
    string? Search,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool? InStock) : IRequest<PagedResponse<ProductSummaryDto>>;
