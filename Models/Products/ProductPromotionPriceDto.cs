namespace foxtrot_store_backend.Models.Products;

public class ProductPromotionPriceDto
{
    public Guid PromotionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string DiscountType { get; set; } = string.Empty;

    public decimal DiscountValue { get; set; }

    public decimal OriginalPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal DiscountedPrice { get; set; }
}