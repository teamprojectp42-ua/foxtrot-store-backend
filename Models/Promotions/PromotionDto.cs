
namespace foxtrot_store_backend.Models.Promotions;

public class PromotionDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? BannerImageUrl { get; set; }

    public int SortOrder { get; set; }
}
