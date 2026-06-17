using Ecommerce.Application.Catalog;
using MediatR;

namespace Ecommerce.Application.Catalog.Queries.GetProductBySlug;

public sealed record GetProductBySlugQuery(string Slug) : IRequest<ProductDetailDto>;
