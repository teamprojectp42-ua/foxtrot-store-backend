using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Auth;

public sealed class UserCultureUpdateRequest
{
    [Required]
    [StringLength(10)]
    public string CultureCode { get; set; } = string.Empty;
}
