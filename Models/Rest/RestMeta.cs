namespace foxtrot_store_backend.Models.Rest;

public class RestMeta
{
    public string ApiName { get; set; } = string.Empty;

    public long ServerTime { get; set; } =
        (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) / 10000;

    public string DataType { get; set; } = "application/json";

    public string[] Manipulations { get; set; } = [];

    public int? CacheTime { get; set; }

    public Dictionary<string, string> Links { get; set; } = [];

    public RestMetaPagination? Pagination { get; set; }

    public Dictionary<string, object> Parameters { get; set; } = [];
}
