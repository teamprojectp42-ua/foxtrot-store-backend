
namespace foxtrot_store_backend.Models.Rest;

public class RestMetaPagination
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;

    public int TotalPages { get; set; }

    public int TotalItems { get; set; }
}
