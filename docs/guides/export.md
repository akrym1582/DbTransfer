# Export database rows

Supply a query inline with `--query`, or read it from a file with `--query-file <path>`. Specify exactly one; DbTransfer passes the resulting text to the provider without rewriting it.
Use `export` to run a source query and stream its rows to a file or standard output. Supported providers are `postgresql`, `sqlserver`, `mysql`, and `oracle`; supported formats are `csv`, `json`, `jsonl`, and `extended-json`.

## First export

Store the connection string in an environment variable, then write JSON Lines to a file:

```sh
export DATABASE='Host=localhost;Database=app;Username=app;Password=secret'

dbtransfer export --provider postgresql --connection "$DATABASE" \
  --query 'select id, created_at, total from public.orders order by id' \
  --format jsonl --output orders.jsonl
```

DbTransfer executes the query without rewriting it. Select columns explicitly when you need a stable exported schema, and add `ORDER BY` when output order matters.

## Choose a format

- `jsonl` writes one JSON object per line and is convenient for streaming tools.
- `json` writes one streaming JSON array.
- `csv` writes a header row. Null is `\N`, while a literal `\N` is escaped as `\\N`.
- `extended-json` writes one object per line and uses wrappers to preserve values such as 64-bit integers, decimals, dates, UUIDs, and binary data.

Use `extended-json` for a DbTransfer export/import round trip when preserving database value types is more important than interoperability with generic JSON tools.

## Write to stdout

`--output -` is the default. Only transferred data is written to stdout; diagnostics and progress use stderr. This makes shell pipelines safe:

```sh
dbtransfer export --provider postgresql --connection "$DATABASE" \
  --query 'select * from public.orders order by id' \
  --format extended-json | gzip > orders.jsonl.gz
```

## Transform each record

Use `--script` for a schema-preserving value change after reading from the database and before formatting output. Use `--script <file.csx>` to read the code from a file, or `--script-text '<C# code>'` to supply it inline. Specify at most one. The script is compiled and run inside DbTransfer; Python or another script runner is not required:

```sh
dbtransfer export --provider postgresql --connection "$DATABASE" \
  --query 'select id, display_name from public.users order by id' \
  --format jsonl --output public-users.jsonl \
  --script ./mask-name.csx --script-argument replacement=hidden
```

`mask-name.csx` can contain:

```csharp
Record["display_name"] = Arguments["replacement"];
```

The script changes values in `Record` and must preserve all property names and original value types. `--script-argument name=value` is optional and repeatable. Compilation errors, exceptions, schema changes, invalid conversions, and oversized results stop the transfer. Scripts run in-process with DbTransfer's permissions, so use only trusted files. See the [full C# script API, result contract, and examples](../csharp-scripts.md).

See the generated [`export` reference](../commands/export.md) for every option and its default.
