namespace foxtrot_store_backend.Models.Cart;

public sealed class CartResponseDto
{
    public List<CartItemDto> Items { get; set; } = [];

    public int TotalAvailableQuantity { get; set; }

    public decimal TotalAmount { get; set; }
}
