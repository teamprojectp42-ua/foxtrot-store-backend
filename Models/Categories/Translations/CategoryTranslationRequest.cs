using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Categories.Translations;

public sealed class CategoryTranslationRequest
{
    [Required]
    [StringLength(10)]
    public string CultureCode { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }
}
