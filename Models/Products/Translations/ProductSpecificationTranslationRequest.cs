using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Products.Translations;

public sealed class ProductSpecificationTranslationRequest
{
    [Required]
    [StringLength(10)]
    public string CultureCode { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Value { get; set; } = string.Empty;
}