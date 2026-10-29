namespace foxtrot_store_backend.Models.Products;

public class ProductCatalogResponseDto
{
    public List<ProductListItemDto> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
