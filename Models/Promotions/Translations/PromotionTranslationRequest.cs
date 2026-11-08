using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Promotions.Translations;

public sealed class PromotionTranslationRequest
{
    [Required]
    [StringLength(10)]
    public string CultureCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }
}