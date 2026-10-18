
using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Products;

public class ProductImageCreateRequest
{
    [Required]
    [StringLength(2048, MinimumLength = 1)]
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }
}
