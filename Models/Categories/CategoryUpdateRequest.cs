using System.ComponentModel.DataAnnotations;
using foxtrot_store_backend.Models.Categories.Translations;

namespace foxtrot_store_backend.Models.Categories;

public class CategoryUpdateRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z0-9]+(?:-[a-zA-Z0-9]+)*$")]
    public string Slug { get; set; } = string.Empty;

    public Guid? ParentCategoryId { get; set; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    [Required]
    [MinLength(1)]
    public List<CategoryTranslationRequest> Translations { get; set; } = [];
}
