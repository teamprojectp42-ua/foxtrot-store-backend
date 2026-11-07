namespace foxtrot_store_backend.Models.Products.Translations;

public sealed class ProductTranslationDto
{
    public string CultureCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}