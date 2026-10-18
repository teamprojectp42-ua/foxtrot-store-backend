
using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Products;

public class ProductCreateRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [StringLength(5000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 999999999)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public Guid? BrandId { get; set; }

    public List<Guid> CategoryIds { get; set; } = [];

    public List<ProductImageCreateRequest> Images { get; set; } = [];

    public List<ProductSpecificationCreateRequest> Specifications { get; set; } = [];
}
