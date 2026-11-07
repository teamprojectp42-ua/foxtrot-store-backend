namespace foxtrot_store_backend.Data.Entities;

public sealed class CategoryTranslation
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public string CultureCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Category Category { get; set; } = null!;
}
