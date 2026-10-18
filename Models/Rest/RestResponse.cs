
namespace foxtrot_store_backend.Models.Rest;

public class RestResponse
{
    public RestStatus Status { get; set; } = RestStatus.Ok;

    public RestMeta Meta { get; set; } = new();

    public object? Data { get; set; }
}
