using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Application.Common.Helpers;
using Ecommerce.Domain.Common;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Admin.Commands.UpdateCategory;

public sealed class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICacheService _cacheService;

    public UpdateCategoryHandler(ICategoryRepository categoryRepository, ICacheService cacheService)
    {
        _categoryRepository = categoryRepository;
        _cacheService = cacheService;
    }

    public async Task Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category not found.");

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugHelper.Generate(request.Name) : request.Slug;

        var existing = await _categoryRepository.GetBySlugAsync(slug, cancellationToken);
        if (existing is not null && existing.Id != category.Id)
            throw new ConflictException("Slug already exists.");

        category.Update(request.Name, slug);

        _categoryRepository.Update(category);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeys.Categories, cancellationToken);
    }
}
