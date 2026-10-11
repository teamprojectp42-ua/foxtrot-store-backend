namespace foxtrot_store_backend.Data.Entities;

public class ProductImage
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;
}