using Microsoft.AspNetCore.Identity;

namespace foxtrot_store_backend.Data.Entities;

public class Cart
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public IdentityUser User { get; set; } = null!;

    public ICollection<CartItem> Items { get; set; } =
        new List<CartItem>();
}
