using MediatR;

namespace Ecommerce.Application.Admin.Commands.CreateCategory;

public sealed record CreateCategoryCommand(string Name, string? Slug) : IRequest<CreateCategoryResponse>;

public sealed record CreateCategoryResponse(Guid Id, string Name, string Slug);
