namespace foxtrot_store_backend.Models.Orders;

public class OrderDto
{
    public Guid Id { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string DeliveryAddress { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<OrderItemDto> Items { get; set; } = [];
}