# Export database rows

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

Use `--script` when every exported row needs a small change, such as masking a name or normalizing a value. The hook runs after DbTransfer reads the row from the database and before it writes the selected output format.

The value of `--script` must be an executable. If your hook is a Python file, for example, use `python3` as the executable and put the file name in `--script-arguments`:

```sh
dbtransfer export --provider postgresql --connection "$DATABASE" \
  --query 'select id, display_name from public.users order by id' \
  --format jsonl --output public-users.jsonl \
  --script python3 --script-arguments './mask-name.py'
```

For example, `mask-name.py` can read the one input object, change a value, and write the object back:

```python
#!/usr/bin/env python3
import json
import sys

record = json.load(sys.stdin)
record["display_name"] = "hidden"
json.dump(record, sys.stdout)
```

For **each row**, DbTransfer performs this exchange:

1. It starts the executable once.
2. It writes one JSON object to the executable's standard input and then closes that input.
3. It reads one JSON object from the executable's standard output and waits for it to exit.
4. It converts the returned values back to the original database column types, then writes the record to the export.

The returned object must contain exactly the same property names in the same order. You may change values, including setting a value to `null`, but you cannot add, remove, rename, or reorder fields. Write only the JSON object to stdout; diagnostic messages from the hook should go to stderr. A non-zero exit, malformed JSON, an incompatible value, a changed schema, or output larger than `--max-batch-bytes` stops the export.

`--script-arguments` is optional. It is useful for passing a script path or settings to the executable, for example `--script-arguments './mask-name.py --replacement hidden'`. DbTransfer starts the executable directly rather than through a shell, so shell features such as pipes, redirection, and environment-variable expansion are not interpreted inside this value.

Because a new process is launched for every row, first try the hook on a small query and measure the performance impact before using it on a large export. The hook is best suited to simple, schema-preserving changes; it cannot filter rows or produce multiple rows from one input row.

See the generated [`export` reference](../commands/export.md) for every option and its default.
