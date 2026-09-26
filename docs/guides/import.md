# Import records into a database

Use `import` to stream records from a file or standard input into a database table. Supported formats are `csv`, `json`, `jsonl`, and `extended-json`; supported destination providers are `postgresql`, `sqlserver`, `mysql`, and `oracle`.

## First import

The input format determines the source columns. For CSV, the first row is the header:

```csv
id,display_name
1,Ada
2,Grace
```

Import it into an existing PostgreSQL table:

```sh
export DATABASE='Host=localhost;Database=app;Username=app;Password=secret'

dbtransfer import --input users.csv --format csv \
  --destination-provider postgresql --destination-connection "$DATABASE" \
  --destination-table public.users
```

Add `--create-table` when the table does not exist. Use `--map source_name=DestinationName` to map input fields to different destination column names; multiple mappings may be comma-separated.

## Read from stdin

`--input -` is the default, so compressed exports can be imported without creating an intermediate uncompressed file:

```sh
gzip -dc orders.jsonl.gz | dbtransfer import --format extended-json \
  --destination-provider postgresql --destination-connection "$DATABASE" \
  --destination-table archive.orders --create-table
```

Make sure the selected `--format` matches the incoming bytes. JSON arrays are parsed incrementally and do not need to fit in memory.

## Nulls and value types

CSV represents null as `\N`, an empty field as an empty string, and a literal `\N` as `\\N`. Extended JSON preserves database-oriented types using explicit wrappers. Plain JSON and JSONL are easier to exchange with other tools but have the normal JSON type limitations.

DbTransfer does not silently truncate strings, round numbers, or infer time zones. A value that the destination cannot accept causes the import to fail.

## Transform each record

Use `--script` for a schema-preserving value change after input parsing and before mappings and destination writes. The value is a C# `.csx` file compiled and run inside DbTransfer; Python or another executable is not required:

```sh
dbtransfer import --input users.jsonl --format jsonl \
  --destination-provider postgresql --destination-connection "$DATABASE" \
  --destination-table public.users --script ./normalize-name.csx
```

`normalize-name.csx` can contain:

```csharp
if (Record["display_name"] is string name)
{
    Record["display_name"] = name.Trim();
}
```

The script changes values in `Record` and must preserve all property names and original value types. `--script-argument name=value` is optional and repeatable; values appear in `Arguments`. Compilation errors, exceptions, schema changes, invalid conversions, and oversized results stop the transfer. With batch transactions, earlier committed batches remain committed. Scripts run in-process with DbTransfer's permissions, so use only trusted files. See the [full C# script API, result contract, and examples](../csharp-scripts.md).

## Transactions and loading mode

The default, `--transaction batch`, commits one batch at a time. `all` requests one transaction for the entire import, and `none` requests no transaction. Native bulk loading is the default; `--no-native-bulk` selects parameterized inserts. Unsupported provider combinations are rejected.

The import command does not provide copy checkpoints or `--resume`. If restartability matters, split input into independently tracked files or use `copy` with checkpoints for database-to-database transfers.

See the generated [`import` reference](../commands/import.md) for every option and its default.
