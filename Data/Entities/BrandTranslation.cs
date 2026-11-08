namespace foxtrot_store_backend.Data.Entities;

public sealed class BrandTranslation
{
    public Guid Id { get; set; }

    public Guid BrandId { get; set; }

    public string CultureCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Brand Brand { get; set; } = null!;
}