
namespace foxtrot_store_backend.Models.Products;

public class ProductDetailsDto
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public string? BrandName { get; set; }

    public List<string> Categories { get; set; } = [];

    public List<ProductImageDto> Images { get; set; } = [];

    public List<ProductSpecificationDto> Specifications { get; set; } = [];
}

public class ProductImageDto
{
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public int SortOrder { get; set; }
}

public class ProductSpecificationDto
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
