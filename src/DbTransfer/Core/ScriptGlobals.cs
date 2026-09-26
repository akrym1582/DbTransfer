namespace DbTransfer.Core;

/// <summary>Values available as top-level names in an in-process C# record script.</summary>
public sealed class ScriptGlobals(
    IDictionary<string, object?> record,
    IReadOnlyDictionary<string, string> arguments,
    CancellationToken cancellationToken)
{
    public IDictionary<string, object?> Record { get; } = record;

    public IReadOnlyDictionary<string, string> Arguments { get; } = arguments;

    public CancellationToken CancellationToken { get; } = cancellationToken;
}
