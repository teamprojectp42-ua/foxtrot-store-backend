using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Orders;

public class CreateOrderRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(30, MinimumLength = 7)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string DeliveryAddress { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Comment { get; set; }
}