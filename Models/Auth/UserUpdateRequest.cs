
using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Auth;

public sealed class UserUpdateRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}
