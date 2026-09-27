# DbTransfer

`DbTransfer` is a .NET 10 command-line application for streaming data between databases and files. The repository contains a provider-neutral transfer foundation, streaming CSV, JSON-array, JSONL, and Extended JSON support, SQL identifier dialects, and live PostgreSQL, SQL Server, MySQL, Oracle, Azure Cosmos DB for NoSQL, MongoDB, and Azure Table Storage import, export, and database-to-database copying.

## Status

Implemented:

- Typed `RecordSchema` and bounded `RecordBatch` models.
- Source and sink connector contracts with capability flags and explicit write outcomes.
- A cancellation-aware transfer engine with bounded batch count and memory budget.
- PostgreSQL, SQL Server, Oracle, and MySQL identifier quoting.
- Streaming UTF-8 JSONL reading and writing with record-size limits.
- Streaming CSV, JSON-array, and Extended JSON import/export.
- Schema-preserving in-process C# record scripts for import and export.
- Production `import` and `export` commands for files and standard streams.
- Implemented `copy`, `export`, `import`, `exec`, `inspect`, and `validate` commands.
- Streaming ADO.NET sources for PostgreSQL, SQL Server, MySQL, and Oracle.
- Native PostgreSQL binary COPY, SQL Server bulk copy, MySQL bulk copy, and Oracle array binding.
- Streaming document reads and bounded bulk writes for Cosmos DB, MongoDB, and Azure Table Storage.
- Optional destination table creation, column mapping, batch/all/no transaction scopes, atomic checkpoints, and batch-based resume.

All commands accept `--log-file` (with `{Date}`, `{UtcDate}`, and `{ProcessId}` placeholders) or `--log-directory`. Logs roll daily and `--log-retention-days` controls automatic cleanup. Transfers report progress to stderr and the log approximately every 10,000 records by default, followed by final counts and elapsed time; use `--progress-interval` to change or disable the interval.

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
│   ├── MySql/                          # MySQL-only implementation
│   ├── CosmosDb/                       # Azure Cosmos DB for NoSQL
│   ├── MongoDb/                        # MongoDB
│   └── AzureTableStorage/              # Azure Table Storage
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

The test suite also contains a PostgreSQL integration test. Set
`DBTRANSFER_POSTGRES_CONNECTION` to a PostgreSQL connection string to enable it; when the
variable is absent, the test is a no-op. GitHub Actions supplies this variable from its
PostgreSQL service container and runs the integration test automatically with the rest of
the suite.

Provider-specific integration workflows also run against disposable SQL Server, MongoDB,
Cosmos DB emulator, and Azurite service containers. To run those tests locally, set
`DBTRANSFER_SQLSERVER_CONNECTION`, `DBTRANSFER_MONGODB_CONNECTION`,
`DBTRANSFER_COSMOSDB_CONNECTION`, or `DBTRANSFER_AZURITE_CONNECTION`, respectively, and
filter on the corresponding integration-test category.

## Run the CLI

Display the available commands:

```sh
dotnet run --project src/DbTransfer -- --help
```

Display help for one command:

```sh
dotnet run --project src/DbTransfer -- copy --help
```

The command roles are:

| Command | Intended role |
|---|---|
| `copy` | Stream records from one database to another. |
| `export` | Stream database records to a file or stdout. |
| `import` | Stream a file or stdin into a database. |
| `exec` | Execute provider-specific SQL and stream result sets. |
| `inspect` | Show schemas, keys, and connector capabilities. |
| `validate` | Validate connectivity and a query without transferring rows. |

The planned data-plane contract reserves stdout for transferred data. Diagnostics, progress, warnings, and summaries belong on stderr so OS pipelines remain safe.

### Import and export

Use `--format csv`, `json`, `jsonl`, or `extended-json`. A path of `-` (the default) selects stdin for imports and stdout for exports:

```sh
dbtransfer export --provider postgresql --connection "$DATABASE" \
  --query 'select id, created_at from events order by id' \
  --format extended-json --output events.jsonl

dbtransfer import --input events.jsonl --format extended-json \
  --destination-provider postgresql --destination-connection "$DATABASE" \
  --destination-table archive.events --create-table
```

JSON-array input is parsed incrementally rather than loaded as a complete document. Extended JSON uses one object per line and preserves `Int64`, `Decimal`, date/time, UUID, and binary values through `$numberLong`, `$numberDecimal`, `$date`, `$uuid`, and `$binary` wrappers. CSV includes a header row, represents null as `\N`, and escapes a literal `\N` as `\\N` so null and empty strings remain distinct.

`--script <file.csx>` or `--script-text '<C# code>'` enables a schema-preserving, in-process C# record hook. DbTransfer ships its C# scripting runtime, compiles the selected code once, and exposes typed `Record`, `Arguments`, and `CancellationToken` globals; Python and external script runners are not required. See the [complete script contract](docs/csharp-scripts.md).

### Database copy

Commands that accept `--query` also accept `--query-file <path>` instead; specify exactly one. The file contents are passed to the provider unchanged.

The `copy` command accepts an unmodified source query and a structured destination name. For example:

```sh
dotnet run --project src/DbTransfer -- copy \
  --source-provider postgresql --source-connection "$SOURCE_DATABASE" \
  --query 'select id, display_name from public.users order by id' \
  --destination-provider sqlserver --destination-connection "$DESTINATION_DATABASE" \
  --destination-table dbo.Users --create-table --map display_name=Name \
  --transaction batch --checkpoint ./copy.checkpoint.json
```

Checkpoints require `--transaction batch`, ensuring every saved marker describes a fully committed batch. Use `--resume` with the same query, endpoints, destination, mappings, and batch settings to skip already committed batches; DbTransfer fingerprints these inputs and rejects mismatched checkpoint files. A resumed `--create-table` transfer reuses the destination created by its first attempt. The query must have deterministic ordering and its source rows must not change between attempts. Native bulk is the default; `--no-native-bulk` selects parameterized inserts.

### Document databases

Document-provider source queries include the source location before a `|` separator. Cosmos DB accepts
`database/container|SELECT * FROM c`, MongoDB accepts `database/collection|{ "active": true }`, and
Azure Table Storage accepts `table|PartitionKey eq 'north'` (an empty expression selects all MongoDB or
Table Storage records). These expressions are sent to the provider without rewriting. Cosmos and MongoDB
destinations use `database.container`; Table Storage uses a one-part table name.

Document stores can contain heterogeneous records. DbTransfer fixes the streamed schema from the first
record and emits `null` for properties absent from later records; nested documents and arrays are retained
as JSON values. Cosmos writes require an `id` property, and Table Storage writes require `PartitionKey`
and `RowKey`; use `--map` when source names differ. New Cosmos containers use `/id` as the partition key.
Because a transfer can span logical partitions, these providers explicitly require `--transaction none`.

See [data providers](docs/providers.md) for connection/query syntax, capability differences, creation behavior, type mappings, and provider-specific limitations.

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

## Publishing a release

Run the **Publish release** workflow manually from the default branch and select the
version component to increment. The workflow tests and packages the project, commits
the updated version, creates a matching tag, and attaches the package to a GitHub
release. It does not publish the package to NuGet.org.
