using Ecommerce.Application.Catalog;
using MediatR;

namespace Ecommerce.Application.Catalog.Queries.GetCategories;

public sealed record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;
