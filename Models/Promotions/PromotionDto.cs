using foxtrot_store_backend.Data.Entities;
using System.Text.Json.Serialization;

namespace foxtrot_store_backend.Models.Promotions;

public class PromotionDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PromotionDiscountType? DiscountType { get; set; }

    public decimal? DiscountValue { get; set; }

    public List<Guid> ProductIds { get; set; } = [];

    public string? BannerImageUrl { get; set; }

    public string? CardImageUrl { get; set; }

    public int SortOrder { get; set; }
}