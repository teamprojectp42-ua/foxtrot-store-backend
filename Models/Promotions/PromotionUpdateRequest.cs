using System.ComponentModel.DataAnnotations;
using foxtrot_store_backend.Data.Entities;
using System.Text.Json.Serialization;

namespace foxtrot_store_backend.Models.Promotions;

public class PromotionUpdateRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [RegularExpression(
        @"^[a-z0-9]+(?:-[a-z0-9]+)*$",
        ErrorMessage = "Slug must contain lowercase letters, numbers, and hyphens only.")]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PromotionDiscountType? DiscountType { get; set; }

    public decimal? DiscountValue { get; set; }

    public List<Guid> ProductIds { get; set; } = [];

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    [Required]
    public bool? IsActive { get; set; }

    public int SortOrder { get; set; }
}
