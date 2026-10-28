
namespace foxtrot_store_backend.Models.Products;

public class ProductListItemDto
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public string? BrandName { get; set; }

    public string? PrimaryImageUrl { get; set; }

    public List<string> Categories { get; set; } = [];

    public ProductPromotionPriceDto? ActivePromotion { get; set; }
}
