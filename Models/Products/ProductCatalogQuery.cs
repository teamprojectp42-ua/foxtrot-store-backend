namespace foxtrot_store_backend.Models.Products;

public class ProductCatalogQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;

    public string? Search { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public Guid? BrandId { get; set; }

    public Guid? CategoryId { get; set; }

    public bool? InStock { get; set; }

    public string SortBy { get; set; } = "name";

    public string SortDirection { get; set; } = "asc";
}
