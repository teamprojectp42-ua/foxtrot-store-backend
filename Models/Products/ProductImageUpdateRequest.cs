using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Products;

public sealed class ProductImageUpdateRequest
{
    [Required]
    public bool? IsPrimary { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int? SortOrder { get; set; }
}