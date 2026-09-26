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

`--script` starts an executable once per record. DbTransfer sends one JSON object to its stdin and expects one JSON object with the same property names and order on stdout:

```sh
dbtransfer export ... --script ./redact-record --script-arguments '--profile public'
```

A non-zero exit, malformed JSON, changed schema, or oversized output stops the transfer. Because a process is launched per row, measure the performance impact before using a record hook on a large export.

See the generated [`export` reference](../commands/export.md) for every option and its default.
