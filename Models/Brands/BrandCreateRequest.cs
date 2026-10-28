
using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Brands;

public class BrandCreateRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z0-9]+(?:-[a-zA-Z0-9]+)*$")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

}
