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

    private ServiceResult(bool isSuccess, TValue? value, ServiceErrorCode? errorCode)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
    }

    /// <summary>Creates a successful result carrying the given value.</summary>
    public static ServiceResult<TValue> Success(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(isSuccess: true, value: value, errorCode: null);
    }

    /// <summary>Creates a failure result carrying the given error code.</summary>
    public static ServiceResult<TValue> Failure(ServiceErrorCode errorCode) =>
        new(isSuccess: false, value: default, errorCode: errorCode);
}
