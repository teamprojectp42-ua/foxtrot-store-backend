namespace foxtrot_store_backend.Data.Entities;

public sealed class PromotionTranslation
{
    public Guid Id { get; set; }

    public Guid PromotionId { get; set; }

    public string CultureCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Promotion Promotion { get; set; } = null!;
}