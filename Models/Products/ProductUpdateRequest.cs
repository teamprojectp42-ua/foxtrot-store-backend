using System.ComponentModel.DataAnnotations;
using foxtrot_store_backend.Models.Products.Translations;

namespace foxtrot_store_backend.Models.Products;

public sealed class ProductUpdateRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Slug { get; set; } = string.Empty;

    [Range(0.01, 999999999)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public Guid? BrandId { get; set; }

    public List<Guid> CategoryIds { get; set; } = [];

    [Required]
    [MinLength(1)]
    public List<ProductTranslationRequest> Translations { get; set; } = [];

    public List<ProductSpecificationUpsertRequest> Specifications
    { get; set; } = [];
}