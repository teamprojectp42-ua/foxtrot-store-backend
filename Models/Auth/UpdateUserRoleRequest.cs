using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Auth;

public sealed class UpdateUserRoleRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;
}