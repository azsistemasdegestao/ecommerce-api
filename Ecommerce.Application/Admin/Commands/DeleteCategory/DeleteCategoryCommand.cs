using MediatR;

namespace Ecommerce.Application.Admin.Commands.DeleteCategory;

public sealed record DeleteCategoryCommand(Guid CategoryId) : IRequest;
