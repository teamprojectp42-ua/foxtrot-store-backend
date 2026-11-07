namespace foxtrot_store_backend.Models.Categories.Translations;

public sealed class CategoryTranslationDto
{
    public string CultureCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
