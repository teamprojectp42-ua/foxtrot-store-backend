using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Cart;

public sealed class CartItemQuantityUpdateRequest
{
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
