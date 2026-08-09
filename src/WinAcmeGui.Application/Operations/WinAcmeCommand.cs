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

    private static string Quote(string value) => value.Any(char.IsWhiteSpace) || value.Contains('"')
        ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
        : value;
}
