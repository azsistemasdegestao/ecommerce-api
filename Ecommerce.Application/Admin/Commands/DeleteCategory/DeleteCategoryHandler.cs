using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Common;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Admin.Commands.DeleteCategory;

public sealed class DeleteCategoryHandler : IRequestHandler<DeleteCategoryCommand>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICacheService _cacheService;

    public DeleteCategoryHandler(ICategoryRepository categoryRepository, ICacheService cacheService)
    {
        _categoryRepository = categoryRepository;
        _cacheService = cacheService;
    }

    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category not found.");

        // BR-ADMIN-014
        if (await _categoryRepository.HasActiveProductsAsync(category.Id, cancellationToken))
            throw new UnprocessableEntityException("Category has active products.");

        category.SoftDelete();

        _categoryRepository.Update(category);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(CacheKeys.Categories, cancellationToken);
    }
}
