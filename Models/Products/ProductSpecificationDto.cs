namespace foxtrot_store_backend.Models.Products;

public sealed class ProductSpecificationDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}