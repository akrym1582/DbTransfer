# DbTransfer contributor instructions

## Repository shape

- Keep all production code in the single `src/DbTransfer/DbTransfer.csproj` project. Tests remain in `tests/DbTransfer.Tests`.
- Put provider-neutral contracts and pipeline code in `src/DbTransfer/Core`.
- Put database-specific code in `src/DbTransfer/Connectors/<Provider>` and use the matching `DbTransfer.Connectors.<Provider>` namespace. Do not combine behavior for multiple databases in one provider file.
- Put serialization and file/standard-stream implementations in `src/DbTransfer/Formats`.
- Keep CLI parsing and composition separate from connector implementation details.

## Design invariants

- Stream records; do not materialize an unbounded result set in a `List`, `DataTable`, `JsonDocument`, or equivalent.
- Use bounded queues and account for bytes as well as record counts. Preserve cancellation and backpressure across every stage.
- Write transferred data only to stdout. Write diagnostics, progress, and summaries to stderr.
- Treat identifiers as structured values, quote every component with the provider dialect, and parameterize data values. Never rewrite arbitrary user SQL.
- Keep provider capabilities explicit. Reject unsupported transaction, bulk, resume, and partial-success combinations instead of silently weakening semantics.
- Do not silently truncate strings, round numbers, infer time zones, or collapse null/missing/empty values.
- Do not put `try`/`catch` around imports.

## Change workflow

1. Read `README.md` and `.agents/skills/dbtransfer-development/SKILL.md` before changing application behavior.
2. Add or update tests for externally observable behavior and provider-specific edge cases.
3. If a verb, option, or help text changes, run `pwsh ./scripts/Generate-CommandDocs.ps1` and commit the updated `docs/commands` files.
4. Run:
   - `dotnet format DbTransfer.slnx --verify-no-changes`
   - `dotnet test DbTransfer.slnx --configuration Release`
   - `git diff --check`
5. Update `README.md` and other hand-written documentation when commands, layout, supported behavior, or limitations change.
