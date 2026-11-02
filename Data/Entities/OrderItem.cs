namespace foxtrot_store_backend.Data.Entities;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string ProductSku { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal TotalPrice { get; set; }

    public Order Order { get; set; } = null!;

    public Product Product { get; set; } = null!;
}