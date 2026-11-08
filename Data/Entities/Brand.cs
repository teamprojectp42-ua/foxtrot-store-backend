namespace foxtrot_store_backend.Data.Entities;

public class Brand
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; }
        = new List<Product>();

    public ICollection<BrandTranslation> Translations { get; set; }
        = new List<BrandTranslation>();
}