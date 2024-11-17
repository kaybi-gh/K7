using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace K7.Server.Application.Helpers;
public static partial class StringParsingHelper
{
    public readonly struct RegexResult(string output, string? trimmedInput)
    {
        public string Output { get; } = output;
        public string? TrimmedInput { get; } = trimmedInput;
    }

    private static bool TryApplyRegex(string input, Regex expression, [NotNullWhen(true)] out RegexResult? result)
    {
        var match = expression.Match(input);
        if (match.Success && match.Groups.TryGetValue("output", out var outputGroup))
        {
            string? trimmedInput;
            if (match.Groups.TryGetValue("trimmedInput", out var trimmedInputGroup))
            {
                trimmedInput = trimmedInputGroup.Value.Trim();
            }
            else
            {
                var noise = match.Groups.GetValueOrDefault("noise")?.Value;
                trimmedInput = string.IsNullOrEmpty(noise) ? null : input.Replace(noise, "").Trim();
            }

            result = new RegexResult(outputGroup.Value.Trim(), trimmedInput);
            return true;
        }
        result = null;
        return false;
    }

    public static bool TryApplyRegexes(string? input, IEnumerable<Regex> regexes, bool recursive, [NotNullWhen(true)] out RegexResult? result)
    {
        result = null;
        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        foreach (var regex in regexes)
        {
            var recursiveInput = result?.TrimmedInput ?? input;
            if (TryApplyRegex(recursiveInput, regex, out var appliedRegexResult))
            {
                result = appliedRegexResult;

                if (!recursive)
                {
                    return true;
                }
            }
        }
        return result != null;
    }
}
