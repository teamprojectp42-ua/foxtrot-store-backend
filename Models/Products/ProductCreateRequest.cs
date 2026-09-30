namespace foxtrot_store_backend.Models.Products;

public class ProductCreateRequest
{
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Description { get; set; } = string.Empty;

    public int StockQuantity { get; set; }
}