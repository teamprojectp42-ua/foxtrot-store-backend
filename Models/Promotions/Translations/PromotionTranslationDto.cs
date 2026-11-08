namespace foxtrot_store_backend.Models.Promotions.Translations;

public sealed class PromotionTranslationDto
{
    public string CultureCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
}