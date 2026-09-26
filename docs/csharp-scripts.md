# In-process C# record scripts

`export` and `import` accept `--script path/to/file.csx`. DbTransfer compiles the C# script once with Roslyn libraries shipped with the application and runs it in the DbTransfer process for every record. Python, `dotnet-script`, a shell, and a separately installed C# compiler are **not** required. A script is trusted code: it has the same operating-system permissions as DbTransfer, so never run an untrusted file.

## Values passed to a script

| Name | Type | Meaning |
|---|---|---|
| `Record` | `IDictionary<string, object?>` | Current record; keys are source names and values retain CLR types. |
| `Arguments` | `IReadOnlyDictionary<string, string>` | Named values from repeatable/comma-separated `--script-argument name=value`. |
| `CancellationToken` | `CancellationToken` | Active transfer cancellation token for awaited operations. |

Common namespaces (`System`, collections, LINQ, threading, and tasks) are imported. Fully qualify other types. Scripts may use top-level statements and `await`.

## Result contract

There is no serialized return value. Change values in `Record`; when execution finishes, DbTransfer reads that dictionary. It must retain exactly the original property names. Adding or removing a property fails. Values must remain assignable or convertible to their original column CLR types, and `null` is preserved. This schema-preserving contract keeps destination validation and bounded streaming predictable; use `--map` to rename destination columns.

The transformed record is measured after execution. A record larger than `--max-batch-bytes` is rejected, and batches are split to stay within that limit. Compilation errors, runtime exceptions, invalid conversions, and schema changes stop the transfer. Scripts execute sequentially in source order; do not keep unbounded static state or start background work.

## Example

Create `normalize-name.csx`:

```csharp
CancellationToken.ThrowIfCancellationRequested();

if (Record["name"] is string name)
{
    Record["name"] = name.Trim();
}

if (Record["email"] is string email)
{
    Record["email"] = Arguments.TryGetValue("redact-domain", out var domain)
        ? $"hidden@{domain}"
        : email.ToLowerInvariant();
}
```

Run it during export:

```sh
dbtransfer export --provider postgresql --connection "$DATABASE" \
  --query 'select name, email from customer order by id' \
  --script ./normalize-name.csx --script-argument redact-domain=example.invalid \
  --format jsonl --output customers.jsonl
```

Or during import (after parsing and before mapping/writing):

```sh
dbtransfer import --input customers.jsonl --format jsonl \
  --script ./normalize-name.csx --script-argument redact-domain=example.invalid \
  --destination-provider sqlserver --destination-connection "$DATABASE" \
  --destination-table dbo.Customer --transaction batch
```

Quote arguments according to your shell. DbTransfer uses the first `=` as separator; names and values must be non-empty, and duplicate names are rejected. Arguments are strings—parse and validate numbers, dates, or other types explicitly.
