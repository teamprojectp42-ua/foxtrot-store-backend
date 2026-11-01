using foxtrot_store_backend.Models.Products;

namespace foxtrot_store_backend.Models.Cart;

public sealed class CartItemDto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal OriginalUnitPrice { get; set; }

    public decimal UnitPrice { get; set; }

    public ProductPromotionPriceDto? ActivePromotion { get; set; }

    public int RequestedQuantity { get; set; }

    public int AvailableQuantity { get; set; }

    public int StockQuantity { get; set; }

    public bool IsProductActive { get; set; }

    public bool IsAvailable { get; set; }

    public bool IsQuantityAdjusted { get; set; }

    public string AvailabilityStatus { get; set; } = string.Empty;

    public decimal LineTotal { get; set; }
}
