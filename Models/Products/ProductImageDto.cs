
namespace foxtrot_store_backend.Models.Products;

public class ProductImageDto
{
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public int SortOrder { get; set; }
}
