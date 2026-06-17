using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Application.Common.Helpers;
using Ecommerce.Domain.Common;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Admin.Commands.CreateCategory;

public sealed class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, CreateCategoryResponse>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICacheService _cacheService;

    public CreateCategoryHandler(ICategoryRepository categoryRepository, ICacheService cacheService)
    {
        _categoryRepository = categoryRepository;
        _cacheService = cacheService;
    }

    public async Task<CreateCategoryResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        // BR-ADMIN-013
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugHelper.Generate(request.Name) : request.Slug;

        // BR-ADMIN-012
        if (await _categoryRepository.GetBySlugAsync(slug, cancellationToken) is not null)
            throw new ConflictException("Slug already exists.");

        var category = Category.Create(request.Name, slug);

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeys.Categories, cancellationToken);

        return new CreateCategoryResponse(category.Id, category.Name, category.Slug);
    }
}
