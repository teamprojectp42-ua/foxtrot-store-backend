namespace foxtrot_store_backend.Data.Entities;

public class PromotionProduct
{
    public Guid PromotionId { get; set; }

    public Guid ProductId { get; set; }

    public Promotion Promotion { get; set; } = null!;

    public Product Product { get; set; } = null!;
}