using System.Globalization;
using System.Text;

namespace CryptoStuff.Cli;

/// <summary>
/// Makes stored text safe to print as text output. Names are refused when
/// they hold a control character, but one stored before that rule, or written
/// by another tool, could still hold one, and printed raw an escape sequence
/// would recolour, move or clear the terminal.
/// </summary>
public static class TerminalText
{
    /// <summary>
    /// Returns <paramref name="text"/> with every control character replaced by
    /// its <c>\uXXXX</c> escape, so <c>ESC</c> prints as <c>\u001b</c>; every
    /// other character is kept as it is.
    /// </summary>
    public static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsControl(character))
            {
                escaped.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
            }
            else
            {
                escaped.Append(character);
            }
        }

        return escaped.ToString();
    }
}
