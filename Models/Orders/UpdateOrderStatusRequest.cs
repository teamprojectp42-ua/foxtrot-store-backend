using System.ComponentModel.DataAnnotations;

namespace foxtrot_store_backend.Models.Orders;

public class UpdateOrderStatusRequest
{
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;
}