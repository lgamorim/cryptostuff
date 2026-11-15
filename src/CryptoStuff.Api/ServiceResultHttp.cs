using CryptoStuff.Core;

namespace CryptoStuff.Api;

/// <summary>Maps a <see cref="ServiceResult{TValue}"/> to the HTTP result the API returns for it.</summary>
internal static class ServiceResultHttp
{
    /// <summary><c>200</c> with the value on success; the failure's ProblemDetails otherwise.</summary>
    public static IResult Ok<TValue>(ServiceResult<TValue> result) =>
        // ServiceResult<T> guarantees Value is non-null when IsSuccess is true, and ErrorCode is set when it's false.
        result.IsSuccess ? Results.Ok(result.Value) : ServiceErrorHttpMapper.ToProblem(result.ErrorCode!.Value, result.ErrorDetail);

    /// <summary>
    /// <c>201</c> with the value and a <c>Location</c> header on success: the
    /// path <paramref name="location"/> gives, under <paramref name="pathBase"/>
    /// so it still names the resource when the API is mounted under a path,
    /// such as behind a reverse proxy. The failure's ProblemDetails otherwise.
    /// </summary>
    public static IResult Created<TValue>(ServiceResult<TValue> result, PathString pathBase, Func<TValue, string> location) =>
        // ServiceResult<T> guarantees Value is non-null when IsSuccess is true, and ErrorCode is set when it's false.
        result.IsSuccess
            ? Results.Created(pathBase.Add(location(result.Value!)).ToString(), result.Value)
            : ServiceErrorHttpMapper.ToProblem(result.ErrorCode!.Value, result.ErrorDetail);

    /// <summary><c>204</c> on success; the failure's ProblemDetails otherwise.</summary>
    public static IResult NoContent(ServiceResult result) =>
        // ServiceResult guarantees ErrorCode is set when IsSuccess is false.
        result.IsSuccess ? Results.NoContent() : ServiceErrorHttpMapper.ToProblem(result.ErrorCode!.Value, result.ErrorDetail);
}
