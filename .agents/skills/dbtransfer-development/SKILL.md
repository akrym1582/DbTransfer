---
name: dbtransfer-development
description: Implement, review, or test changes in the DbTransfer .NET CLI repository. Use for transfer-pipeline work, database connector additions, SQL dialect behavior, streaming CSV/JSON/JSONL formats, CLI verbs/options, transaction semantics, mapping, checkpoints, and related tests or documentation.
---

# DbTransfer development

## Inspect first

1. Read `/workspace/DbTransfer/AGENTS.md` and the relevant source and tests.
2. Check `git status` before editing; preserve unrelated work.
3. Identify whether the change is shared core, provider-specific, format-specific, or CLI composition.

## Place code

- Keep exactly one non-test project: `src/DbTransfer/DbTransfer.csproj`.
- Put shared records, contracts, planning, and pipeline logic in `src/DbTransfer/Core`.
- Put a provider implementation in `src/DbTransfer/Connectors/<Provider>` with namespace `DbTransfer.Connectors.<Provider>`.
- Put byte/file/stdin/stdout formats in `src/DbTransfer/Formats`.
- Put unit tests in `tests/DbTransfer.Tests`; mirror provider boundaries in test names or folders as the suite grows.

## Implement safely

- Prefer `IAsyncEnumerable<T>` for sequential streaming and bounded `Channel<T>` only when stages must overlap.
- Carry cancellation tokens through async I/O.
- Bound both queued work and estimated bytes. Never buffer the full transfer.
- Preserve typed values between databases; serialize only at file or standard-stream boundaries.
- Represent unsupported provider behavior as validation errors. Do not silently fall back when correctness, transactionality, constraints, triggers, ordering, or retry safety changes.
- Keep stdout data-only and send all diagnostics to stderr.
- Add provider-specific types only to that provider's namespace, while implementing shared interfaces from Core.

## Verify

Run from the repository root:

```sh
dotnet format DbTransfer.slnx
dotnet test DbTransfer.slnx --configuration Release
dotnet format DbTransfer.slnx --verify-no-changes
git diff --check
```

For CLI changes, also run the affected verb via:

```sh
dotnet run --project src/DbTransfer -- <arguments>
```

Test malformed input, cancellation, boundaries, and provider-specific escaping or capability rejection where relevant. Update `README.md` if user-visible behavior or project organization changes.

When changing a CLI verb, option, default, or help text, regenerate the checked-in command reference:

```sh
pwsh ./scripts/Generate-CommandDocs.ps1
git diff --exit-code -- docs/commands
```

Treat a documentation diff as part of the CLI change and commit it with the implementation and tests.
