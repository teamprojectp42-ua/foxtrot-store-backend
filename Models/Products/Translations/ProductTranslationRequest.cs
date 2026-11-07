using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Products.Translations;

public sealed class ProductTranslationRequest
{
    [Required]
    [StringLength(10)]
    public string CultureCode { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(5000)]
    public string Description { get; set; } = string.Empty;
}