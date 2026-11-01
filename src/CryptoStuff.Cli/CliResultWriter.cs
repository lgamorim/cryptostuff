using CryptoStuff.Core;

namespace CryptoStuff.Cli;

/// <summary>
/// Writes a command's outcome to the injected writers and returns the exit
/// code that outcome maps to, so every command family reports results, usage,
/// and errors the same way.
/// </summary>
public sealed class CliResultWriter
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    /// <summary>Creates a writer that sends results to <paramref name="output"/> and everything else to <paramref name="error"/>.</summary>
    public CliResultWriter(TextWriter output, TextWriter error)
    {
        _output = output;
        _error = error;
    }

    /// <summary>
    /// Formats a successful <paramref name="result"/> with <paramref name="format"/>
    /// and writes it to the output, returning <c>0</c>; writes a failed one as an
    /// error line, returning <c>1</c>.
    /// </summary>
    public int WriteServiceResult<TView>(ServiceResult<TView> result, bool json, Func<TView, bool, string> format) =>
        // ServiceResult<T> guarantees Value is non-null when IsSuccess is true, and ErrorCode is set when it's false.
        result.IsSuccess
            ? WriteResult(format(result.Value!, json))
            : WriteError(ServiceErrorMessages.For(result.ErrorCode!.Value, result.ErrorDetail));

    /// <summary>
    /// Writes <paramref name="formattedSuccess"/> to the output, returning <c>0</c>,
    /// when <paramref name="result"/> succeeded; writes a failed one as an error
    /// line, returning <c>1</c>.
    /// </summary>
    public int WriteServiceResult(ServiceResult result, string formattedSuccess) =>
        // ServiceResult guarantees ErrorCode is set when IsSuccess is false.
        result.IsSuccess
            ? WriteResult(formattedSuccess)
            : WriteError(ServiceErrorMessages.For(result.ErrorCode!.Value, result.ErrorDetail));

    /// <summary>Writes <see cref="CliUsage.Message"/> to the error writer and returns <c>1</c>.</summary>
    public int WriteUsage()
    {
        _error.Write(CliUsage.Message);
        _error.Write('\n');
        return 1;
    }

    /// <summary>Writes <paramref name="message"/> to the error writer and returns <c>1</c>.</summary>
    public int WriteError(string message)
    {
        _error.Write(message);
        _error.Write('\n');
        return 1;
    }

    /// <summary>Writes <paramref name="formatted"/> to the output writer and returns <c>0</c>.</summary>
    public int WriteResult(string formatted)
    {
        _output.Write(formatted);
        _output.Write('\n');
        return 0;
    }
}
