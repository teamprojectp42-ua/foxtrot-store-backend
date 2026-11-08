using System.ComponentModel.DataAnnotations;
using foxtrot_store_backend.Models.Brands.Translations;

namespace foxtrot_store_backend.Models.Brands;

public class BrandCreateRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z0-9]+(?:-[a-zA-Z0-9]+)*$")]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<BrandTranslationRequest> Translations { get; set; } = [];
}