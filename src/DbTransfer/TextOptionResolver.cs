using System.IO;

/// <summary>Resolves mutually exclusive inline and file-backed command text.</summary>
public static class TextOptionResolver
{
    public static async Task<string> ResolveRequiredAsync(
        string? inlineText,
        string? filePath,
        string optionName,
        CancellationToken cancellationToken = default)
    {
        var hasInlineText = !string.IsNullOrWhiteSpace(inlineText);
        var hasFilePath = !string.IsNullOrWhiteSpace(filePath);
        if (hasInlineText == hasFilePath)
        {
            throw new ArgumentException($"Specify exactly one of --{optionName} or --{optionName}-file.");
        }

        return hasFilePath
            ? await File.ReadAllTextAsync(filePath!, cancellationToken).ConfigureAwait(false)
            : inlineText!;
    }

    public static async Task<string?> ResolveOptionalAsync(
        string? inlineText,
        string? filePath,
        string inlineOptionName,
        string fileOptionName,
        CancellationToken cancellationToken = default)
    {
        var hasInlineText = !string.IsNullOrWhiteSpace(inlineText);
        var hasFilePath = !string.IsNullOrWhiteSpace(filePath);
        if (hasInlineText && hasFilePath)
        {
            throw new ArgumentException($"Specify only one of --{inlineOptionName} or --{fileOptionName}.");
        }

        if (hasFilePath)
        {
            return await File.ReadAllTextAsync(filePath!, cancellationToken).ConfigureAwait(false);
        }

        return hasInlineText ? inlineText : null;
    }
}
