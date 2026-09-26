# DbTransfer

`DbTransfer` is an early .NET 10 command-line application for streaming data between databases and files. The current repository contains the provider-neutral transfer foundation, JSONL support, SQL identifier dialects, and the CLI command surface. Database connections and native bulk writers are not implemented yet.

## Status

Implemented:

- Typed `RecordSchema` and bounded `RecordBatch` models.
- Source and sink connector contracts with capability flags and explicit write outcomes.
- A cancellation-aware transfer engine with bounded batch count and memory budget.
- PostgreSQL, SQL Server, Oracle, and MySQL identifier quoting.
- Streaming UTF-8 JSONL reading and writing with record-size limits.
- CLI registration for `copy`, `export`, `import`, `exec`, `inspect`, and `validate`.

Not implemented yet:

- Live database connectors and authentication.
- Native bulk APIs, table creation, mapping, transactions, checkpoints, and resume.
- CSV, JSON-array, Extended JSON, scripting hooks, and production import/export commands.

Recognized commands currently return exit code `3` and explain on stderr that no database connector is installed.

## Repository layout

```text
src/DbTransfer/                         # the single production project
├── DbTransfer.csproj
├── Program.cs                          # CLI entry point and options
├── Core/                               # provider-neutral records and pipeline
├── Connectors/
│   ├── PostgreSql/                     # PostgreSQL-only implementation
│   ├── SqlServer/                      # Microsoft SQL Server-only implementation
│   ├── Oracle/                         # Oracle-only implementation
│   └── MySql/                          # MySQL-only implementation
└── Formats/                            # file and standard-stream formats
tests/DbTransfer.Tests/                 # separate xUnit test project
.agents/skills/dbtransfer-development/  # repository development workflow skill
AGENTS.md                               # contributor and coding constraints
docs/                                   # guides and generated command reference
scripts/Generate-CommandDocs.ps1        # command documentation generator
```

All production code intentionally compiles from one `csproj`. Provider-specific behavior stays in a dedicated connector folder and namespace so, for example, SQL Server and PostgreSQL code do not become interleaved with the shared transfer engine.

## Prerequisites

- .NET SDK 10.0 or later.

## Build and test

```sh
dotnet restore DbTransfer.slnx
dotnet build DbTransfer.slnx --configuration Release
dotnet test DbTransfer.slnx --configuration Release
dotnet format DbTransfer.slnx --verify-no-changes
```

## Run the CLI

Display the available commands:

```sh
dotnet run --project src/DbTransfer -- --help
```

Display help for one command:

```sh
dotnet run --project src/DbTransfer -- copy --help
```

The planned command roles are:

| Command | Intended role |
|---|---|
| `copy` | Stream records from one database to another. |
| `export` | Stream database records to a file or stdout. |
| `import` | Stream a file or stdin into a database. |
| `exec` | Execute provider-specific SQL and stream result sets. |
| `inspect` | Show schemas, keys, and connector capabilities. |
| `validate` | Validate connectivity and a transfer plan without writing. |

The planned data-plane contract reserves stdout for transferred data. Diagnostics, progress, warnings, and summaries belong on stderr so OS pipelines remain safe.

## Architecture rules

- Do not load complete transfers into memory. Use `IAsyncEnumerable<T>` for the basic path and bounded channels only where overlapping stages is beneficial.
- Bound buffering by estimated bytes as well as batch count, and propagate cancellation through producers and consumers.
- Keep values typed for database-to-database copies; serialization belongs at file/stdin/stdout boundaries.
- Keep shared contracts in `Core` and database-specific behavior in `Connectors/<Provider>`.
- Quote structured identifier components through the selected SQL dialect and parameterize values. User-provided SQL is never rewritten.
- Report unsupported combinations rather than silently weakening bulk, transaction, retry, ordering, or consistency semantics.

See [`AGENTS.md`](AGENTS.md) for contribution rules. Agents implementing repository changes should also use [the DbTransfer development skill](.agents/skills/dbtransfer-development/SKILL.md).

## Documentation

- Start with the [`docs` index](docs/README.md) for guides and command examples.
- The files under `docs/commands` are generated from the CLI's actual `--help` output. Do not edit them by hand.
- After changing verbs, options, defaults, or help text, regenerate them with PowerShell:

  ```powershell
  pwsh ./scripts/Generate-CommandDocs.ps1
  ```
