using MediatR;

namespace Ecommerce.Application.Admin.Commands.UpdateCategory;

public sealed record UpdateCategoryCommand(Guid CategoryId, string Name, string? Slug) : IRequest;
