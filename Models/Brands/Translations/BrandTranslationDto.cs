namespace foxtrot_store_backend.Models.Brands.Translations;

public sealed class BrandTranslationDto
{
    public string CultureCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}