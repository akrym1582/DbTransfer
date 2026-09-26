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

Use `--script` when each input record needs a small change before it reaches the database, such as trimming a value or replacing a placeholder. The hook runs after DbTransfer parses the input format and before column mappings and destination writes are applied.

The value of `--script` must be an executable. This example runs a Python file for every input record:

```sh
dbtransfer import --input users.jsonl --format jsonl \
  --destination-provider postgresql --destination-connection "$DATABASE" \
  --destination-table public.users \
  --script python3 --script-arguments './normalize-name.py'
```

The file `normalize-name.py` could contain:

```python
#!/usr/bin/env python3
import json
import sys

record = json.load(sys.stdin)
record["display_name"] = record["display_name"].strip()
json.dump(record, sys.stdout)
```

For **each record**, DbTransfer starts the executable, sends one JSON object to its standard input, closes the input, and reads one JSON object from standard output. The returned object must have exactly the same property names in the same order as the input object. Values may change, including to `null`, but fields cannot be added, removed, renamed, or reordered. Returned values also need to be convertible to the types DbTransfer discovered from the input.

Write only the returned JSON object to stdout; send hook diagnostics to stderr. A non-zero exit, malformed JSON, an incompatible value, a changed schema, or output larger than `--max-batch-bytes` stops the import. If batch transactions are in use, previously committed batches remain committed.

`--script-arguments` is optional and passes arguments to the executable. For example, `--script python3 --script-arguments './normalize-name.py --mode strict'` runs Python with the script path and its setting. DbTransfer starts the executable directly rather than through a shell, so pipes, redirection, and environment-variable expansion are not interpreted inside `--script-arguments`.

Try the hook on a small input first. DbTransfer starts a new process for every record, so this feature can be slow on a large import. It is intended for schema-preserving value changes; it cannot skip a record or turn one input record into several records.

## Transactions and loading mode

The default, `--transaction batch`, commits one batch at a time. `all` requests one transaction for the entire import, and `none` requests no transaction. Native bulk loading is the default; `--no-native-bulk` selects parameterized inserts. Unsupported provider combinations are rejected.

The import command does not provide copy checkpoints or `--resume`. If restartability matters, split input into independently tracked files or use `copy` with checkpoints for database-to-database transfers.

See the generated [`import` reference](../commands/import.md) for every option and its default.
