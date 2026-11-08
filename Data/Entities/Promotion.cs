namespace foxtrot_store_backend.Data.Entities;

public class Promotion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Slug { get; set; } = string.Empty;

    public string? BannerImageUrl { get; set; }

    public string? CardImageUrl { get; set; }

    public string? ImageUrl { get; set; }

    public PromotionDiscountType? DiscountType { get; set; }

    public decimal? DiscountValue { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<PromotionProduct> PromotionProducts { get; set; }
        = new List<PromotionProduct>();

    public ICollection<PromotionTranslation> Translations { get; set; }
        = new List<PromotionTranslation>();
}