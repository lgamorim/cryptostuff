namespace CryptoStuff.Core;

/// <summary>
/// The outcome of a Core operation: either a value on success, or a
/// <see cref="ServiceErrorCode"/> describing why it failed.
/// </summary>
public sealed record ServiceResult<TValue>
{
    /// <summary>Whether the operation completed successfully.</summary>
    public bool IsSuccess { get; }

    /// <summary>The resulting value. Null when <see cref="IsSuccess"/> is false.</summary>
    public TValue? Value { get; }

    /// <summary>The failure reason. Null when <see cref="IsSuccess"/> is true.</summary>
    public ServiceErrorCode? ErrorCode { get; }

    /// <summary>
    /// A message explaining the failure, preferred over the fixed text for
    /// <see cref="ErrorCode"/> when present. Null when none was given.
    /// </summary>
    public string? ErrorDetail { get; }

    private ServiceResult(bool isSuccess, TValue? value, ServiceErrorCode? errorCode, string? errorDetail)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        ErrorDetail = errorDetail;
    }

    /// <summary>Creates a successful result carrying the given value.</summary>
    public static ServiceResult<TValue> Success(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(isSuccess: true, value: value, errorCode: null, errorDetail: null);
    }

    /// <summary>
    /// Creates a failure result carrying the given error code and, optionally, a detail message.
    /// A null or whitespace-only <paramref name="errorDetail"/> is stored as null.
    /// </summary>
    public static ServiceResult<TValue> Failure(ServiceErrorCode errorCode, string? errorDetail = null) =>
        new(isSuccess: false, value: default, errorCode: errorCode, errorDetail: ServiceResult.NormalizeDetail(errorDetail));
}
