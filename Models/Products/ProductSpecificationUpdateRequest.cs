using System.ComponentModel.DataAnnotations;
using foxtrot_store_backend.Models.Products.Translations;

namespace foxtrot_store_backend.Models.Products;

public sealed class ProductSpecificationUpdateRequest
{
    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }

    [Required]
    [MinLength(1)]
    public List<ProductSpecificationTranslationRequest> Translations
    { get; set; } = [];
}