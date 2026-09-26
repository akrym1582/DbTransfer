# Documentation

## Start here

If this is your first time using DbTransfer, choose the task you want to perform. Each guide explains the command's role, a safe first run, practical examples, and important operational details:

- [`copy`: move data directly between databases](guides/copy.md)
- [`export`: write query results to a file or stdout](guides/export.md)
- [`import`: load file or stdin records into a database](guides/import.md)
- [`exec`: run SQL and serialize its result rows](guides/exec.md)
- [`inspect`: preview a query's result schema](guides/inspect.md)
- [`validate`: test connectivity and query preparation](guides/validate.md)

Connection strings can contain passwords. Prefer environment variables such as `$DATABASE` rather than placing credentials directly in shell history. DbTransfer writes transferred data to stdout and progress or errors to stderr, so piping command output remains safe.

The `export` and `import` commands also support `--script`, which lets a small executable change each record while it is moving through DbTransfer. See the **Transform each record** section in the [export guide](guides/export.md#transform-each-record) or [import guide](guides/import.md#transform-each-record) for a beginner-friendly example. The other commands do not offer this option.

## Generated command reference

The command pages contain generated `--help` output and concise usage examples. When a matching beginner guide exists under `docs/guides`, the generator adds a link to it automatically:

- [`copy`](commands/copy.md)
- [`export`](commands/export.md)
- [`import`](commands/import.md)
- [`exec`](commands/exec.md)
- [`inspect`](commands/inspect.md)
- [`validate`](commands/validate.md)

The examples describe the implemented CLI. Generate all command pages from the actual executable with:

```powershell
pwsh ./scripts/Generate-CommandDocs.ps1
```

Files in `docs/commands` are generated. Change CLI attributes, the examples, or the generator instead of editing a generated page directly. The explanatory pages in `docs/guides` are hand-written and can be edited normally.
