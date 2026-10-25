namespace TimeBank.Application.DTOs.Categories;

public sealed record SkillDto(int Id, string Name, int CategoryId, string CategoryName);

public sealed record CategoryDto(int Id, string Name, string? Description, IReadOnlyList<SkillDto> Skills);

public sealed class CategoryCreateRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Description { get; set; }
}

public sealed class CategoryUpdateRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Description { get; set; }
}
