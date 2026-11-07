namespace foxtrot_store_backend.Models.Products.Translations;

public sealed class ProductSpecificationTranslationDto
{
    public string CultureCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}