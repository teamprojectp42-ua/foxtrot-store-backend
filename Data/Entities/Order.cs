using Microsoft.AspNetCore.Identity;

namespace foxtrot_store_backend.Data.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string DeliveryAddress { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.New;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public IdentityUser User { get; set; } = null!;

    public ICollection<OrderItem> Items { get; set; } =
        new List<OrderItem>();
}