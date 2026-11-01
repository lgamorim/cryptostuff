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
    /// <c>201</c> with the value and a <c>Location</c> header from <paramref name="location"/> on success;
    /// the failure's ProblemDetails otherwise.
    /// </summary>
    public static IResult Created<TValue>(ServiceResult<TValue> result, Func<TValue, string> location) =>
        // ServiceResult<T> guarantees Value is non-null when IsSuccess is true, and ErrorCode is set when it's false.
        result.IsSuccess
            ? Results.Created(location(result.Value!), result.Value)
            : ServiceErrorHttpMapper.ToProblem(result.ErrorCode!.Value, result.ErrorDetail);

    /// <summary><c>204</c> on success; the failure's ProblemDetails otherwise.</summary>
    public static IResult NoContent(ServiceResult result) =>
        // ServiceResult guarantees ErrorCode is set when IsSuccess is false.
        result.IsSuccess ? Results.NoContent() : ServiceErrorHttpMapper.ToProblem(result.ErrorCode!.Value, result.ErrorDetail);
}
