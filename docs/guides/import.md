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

## Transactions and loading mode

The default, `--transaction batch`, commits one batch at a time. `all` requests one transaction for the entire import, and `none` requests no transaction. Native bulk loading is the default; `--no-native-bulk` selects parameterized inserts. Unsupported provider combinations are rejected.

The import command does not provide copy checkpoints or `--resume`. If restartability matters, split input into independently tracked files or use `copy` with checkpoints for database-to-database transfers.

See the generated [`import` reference](../commands/import.md) for every option and its default.
