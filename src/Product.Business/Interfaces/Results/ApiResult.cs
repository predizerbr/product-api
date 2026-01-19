using Microsoft.AspNetCore.Http;

namespace Product.Business.Interfaces.Results;

public record ApiResult(
    int StatusCode,
    object? Data = null,
    string? Error = null,
    object? Meta = null,
    bool Envelope = false
)
{
    public static ApiResult Ok(object? data, bool envelope = false, object? meta = null) =>
        new(StatusCodes.Status200OK, data, null, meta, envelope);

    public static ApiResult NoContent() => new(StatusCodes.Status204NoContent);

    public static ApiResult Problem(int statusCode, string error) => new(statusCode, null, error);

    public static ApiResult ErrorBody(int statusCode, object? body) => new(statusCode, body);
}
