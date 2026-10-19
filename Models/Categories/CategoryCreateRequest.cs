
using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Categories;

public class CategoryCreateRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z0-9]+(?:-[a-zA-Z0-9]+)*$")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public Guid? ParentCategoryId { get; set; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }
}
