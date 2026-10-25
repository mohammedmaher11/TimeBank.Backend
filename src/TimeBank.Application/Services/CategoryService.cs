using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Categories;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Application.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public CategoryService(IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<List<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _unitOfWork.Categories.GetAllAsync(cancellationToken);

    public async Task<CategoryDto> CreateAsync(
        CategoryCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _unitOfWork.Categories.NameExistsAsync(request.Name, null, cancellationToken))
            throw new ConflictException("A category with this name already exists.");

        var category = Category.Create(request.Name, request.Description, _clock.UtcNow);
        await _unitOfWork.Categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.Name, category.Description, []);
    }

    public async Task<CategoryDto> UpdateAsync(
        int id, CategoryUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        if (await _unitOfWork.Categories.NameExistsAsync(request.Name, id, cancellationToken))
            throw new ConflictException("A category with this name already exists.");

        category.Update(request.Name, request.Description);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await _unitOfWork.Categories.GetDtoByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        if (await _unitOfWork.Categories.HasRequestsAsync(id, cancellationToken))
            throw new ConflictException("This category has requests and cannot be deleted.");

        if (await _unitOfWork.Categories.HasMemberSkillsAsync(id, cancellationToken))
            throw new ConflictException("This category has skills assigned to members and cannot be deleted.");

        _unitOfWork.Categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
