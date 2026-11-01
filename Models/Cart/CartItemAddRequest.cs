using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Cart;

public sealed class CartItemAddRequest
{
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;
}
