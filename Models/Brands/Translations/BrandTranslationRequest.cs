using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Brands.Translations;

public sealed class BrandTranslationRequest
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