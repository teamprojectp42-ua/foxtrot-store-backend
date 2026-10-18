
using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Products;

public class ProductSpecificationCreateRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Value { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; }
}
