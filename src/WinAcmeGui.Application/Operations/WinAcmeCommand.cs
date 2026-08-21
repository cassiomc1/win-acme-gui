using WinAcmeGui.Domain.Operations;

namespace WinAcmeGui.Application.Operations;

public sealed record WinAcmeCommand(
    string ExecutablePath,
    IReadOnlyList<SensitiveArgument> Arguments)
{
    public string DisplayText => string.Join(' ', new[] { Quote(ExecutablePath) }.Concat(
        Arguments.Select(argument => argument.Value.Length == 0
            ? argument.Name
            : $"{argument.Name} {Quote(argument.DisplayValue)}")));

    /// <summary>Never render raw values: one debug/log print must not leak secret arguments.</summary>
    public override string ToString() => DisplayText;

    // Mirrors the MSVCRT/CommandLineToArgvW quoting rules so the preview users confirm matches what
    // a console would parse: backslashes preceding a quote (including trailing ones) are doubled.
    private static string Quote(string value)
    {
        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"')) return value;
        var builder = new System.Text.StringBuilder(value.Length + 8).Append('"');
        var backslashes = 0;
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    backslashes++;
                    builder.Append('\\');
                    break;
                case '"':
                    builder.Append('\\', backslashes + 1).Append('"');
                    backslashes = 0;
                    break;
                default:
                    builder.Append(character);
                    backslashes = 0;
                    break;
            }
        }
        builder.Append('\\', backslashes).Append('"');
        return builder.ToString();
    }
}
