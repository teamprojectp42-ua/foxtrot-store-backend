namespace foxtrot_store_backend.Data.Entities;

public sealed class ProductTranslation
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string CultureCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Product Product { get; set; } = null!;
}