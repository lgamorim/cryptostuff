namespace CryptoStuff.Core;

/// <summary>
/// The outcome of a Core operation that has nothing to return: either success,
/// or a <see cref="ServiceErrorCode"/> describing why it failed.
/// </summary>
public sealed record ServiceResult
{
    /// <summary>Whether the operation completed successfully.</summary>
    public bool IsSuccess { get; }

    /// <summary>The failure reason. Null when <see cref="IsSuccess"/> is true.</summary>
    public ServiceErrorCode? ErrorCode { get; }

    /// <summary>
    /// A message explaining the failure, preferred over the fixed text for
    /// <see cref="ErrorCode"/> when present. Null when none was given.
    /// </summary>
    public string? ErrorDetail { get; }

    private ServiceResult(bool isSuccess, ServiceErrorCode? errorCode, string? errorDetail)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorDetail = errorDetail;
    }

    /// <summary>Creates a successful result.</summary>
    public static ServiceResult Success() => new(isSuccess: true, errorCode: null, errorDetail: null);

    /// <summary>
    /// Creates a failure result carrying the given error code and, optionally, a detail message.
    /// A null or whitespace-only <paramref name="errorDetail"/> is stored as null.
    /// </summary>
    public static ServiceResult Failure(ServiceErrorCode errorCode, string? errorDetail = null) =>
        new(isSuccess: false, errorCode: errorCode, errorDetail: NormalizeDetail(errorDetail));

    internal static string? NormalizeDetail(string? errorDetail) =>
        string.IsNullOrWhiteSpace(errorDetail) ? null : errorDetail;
}
