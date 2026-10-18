
namespace foxtrot_store_backend.Models.Rest;

public class RestStatus
{
    public bool IsOk { get; set; }

    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;

    public static readonly RestStatus Ok = new()
    {
        IsOk = true,
        Code = 200,
        Message = "OK"
    };

    public static readonly RestStatus BadRequest = new()
    {
        IsOk = false,
        Code = 400,
        Message = "Bad Request"
    };

    public static readonly RestStatus Unauthorized = new()
    {
        IsOk = false,
        Code = 401,
        Message = "Unauthorized"
    };

    public static readonly RestStatus Forbidden = new()
    {
        IsOk = false,
        Code = 403,
        Message = "Forbidden"
    };

    public static readonly RestStatus NotFound = new()
    {
        IsOk = false,
        Code = 404,
        Message = "Not Found"
    };

    public static readonly RestStatus MethodNotAllowed = new()
    {
        IsOk = false,
        Code = 405,
        Message = "Method Not Allowed"
    };

    public static readonly RestStatus Conflict = new()
    {
        IsOk = false,
        Code = 409,
        Message = "Conflict"
    };

    public static readonly RestStatus UnsupportedMediaType = new()
    {
        IsOk = false,
        Code = 415,
        Message = "Unsupported Media Type"
    };

    public static readonly RestStatus TooManyRequests = new()
    {
        IsOk = false,
        Code = 429,
        Message = "Too Many Requests"
    };

    public static readonly RestStatus InternalServerError = new()
    {
        IsOk = false,
        Code = 500,
        Message = "Internal Server Error"
    };

    public static readonly RestStatus NotImplemented = new()
    {
        IsOk = false,
        Code = 501,
        Message = "Not Implemented"
    };

    public static readonly RestStatus BadGateway = new()
    {
        IsOk = false,
        Code = 502,
        Message = "Bad Gateway"
    };

    public static readonly RestStatus ServiceUnavailable = new()
    {
        IsOk = false,
        Code = 503,
        Message = "Service Unavailable"
    };

    public static readonly RestStatus HeaderRequired = new()
    {
        IsOk = false,
        Code = 440,
        Message = "Header Required"
    };

    public static readonly RestStatus HeaderMalformed = new()
    {
        IsOk = false,
        Code = 441,
        Message = "Header Malformed"
    };
}
