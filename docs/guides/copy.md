# Copy data between databases

Use `copy` when the source and destination are both databases. Rows are streamed from the source query into the destination table, so DbTransfer does not need to hold the complete result set in memory.

## Before you begin

You need a connection string for each database and permission to run the source query and write the destination table. Supported provider names are `postgresql`, `sqlserver`, `mysql`, and `oracle`.

DbTransfer executes `--query` exactly as supplied. Include a deterministic `ORDER BY` when repeatability matters, especially when using checkpoints. The structured `--destination-table` value accepts `name`, `schema.name`, or `catalog.schema.name`; DbTransfer quotes its components for the destination provider.

## First copy

This example copies selected PostgreSQL columns into an existing SQL Server table:

```sh
export SOURCE_DATABASE='Host=localhost;Database=app;Username=app;Password=secret'
export DESTINATION_DATABASE='Server=localhost;Database=warehouse;User Id=app;Password=secret;TrustServerCertificate=true'

dbtransfer copy \
  --source-provider postgresql --source-connection "$SOURCE_DATABASE" \
  --query 'select id, display_name from public.users order by id' \
  --destination-provider sqlserver --destination-connection "$DESTINATION_DATABASE" \
  --destination-table dbo.Users
```

Add `--create-table` when the destination table does not exist. To rename incoming columns, pass comma-separated mappings:

```sh
dbtransfer copy ... --destination-table dbo.Users --create-table \
  --map id=UserId,display_name=DisplayName
```

Unmapped columns keep their source names.

## Transactions and bulk loading

`--transaction batch` is the default and commits each completed batch. `all` wraps the full transfer in one transaction, while `none` does not request a transaction. Provider capabilities differ; unsupported combinations fail rather than silently changing the requested guarantees.

Native provider bulk loading is enabled by default. Use `--no-native-bulk` only when you intentionally want parameterized inserts, for example while diagnosing bulk-loader behavior.

## Resume an interrupted copy

Checkpoints record fully committed batches and therefore require batch transactions:

```sh
dbtransfer copy ... --transaction batch --checkpoint ./users.checkpoint.json
dbtransfer copy ... --transaction batch --checkpoint ./users.checkpoint.json --resume
```

Use exactly the same endpoints, query, destination, mappings, and batch settings for the resumed command. DbTransfer rejects a checkpoint whose transfer fingerprint differs. The source query must have stable ordering, and the source rows must not change between attempts; resume skips committed batches rather than rewriting the SQL query.

## Operational checks

- Start with [`inspect`](inspect.md) to confirm the columns and data types returned by the source query.
- Use a small, filtered query for a first run and verify the destination before removing the filter.
- Set `--progress-interval 0` to disable progress messages, or choose a positive record interval.
- Logs and progress go to stderr. Use `--log-file` or `--log-directory` to retain diagnostics.

See the generated [`copy` reference](../commands/copy.md) for every option and its default.
