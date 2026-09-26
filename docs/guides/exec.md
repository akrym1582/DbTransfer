# Execute SQL and stream result rows

Use `exec` when you want to supply provider-specific SQL and serialize the rows it returns. Unlike `export`, the option is named `--sql` to emphasize that the statement is passed through unchanged.

## First execution

```sh
export DATABASE='Host=localhost;Database=app;Username=app;Password=secret'

dbtransfer exec --provider postgresql --connection "$DATABASE" \
  --sql 'select id, total from public.orders order by id' \
  --format jsonl --output orders.jsonl
```

Supported provider names are `postgresql`, `sqlserver`, `mysql`, and `oracle`. Output formats are `csv`, `json`, `jsonl`, and `extended-json`. `--output -` writes to stdout and is the default.

## Pipe the result

Transferred rows go to stdout while diagnostics go to stderr:

```sh
dbtransfer exec --provider postgresql --connection "$DATABASE" \
  --sql 'select id from public.orders where status = ''open'' order by id' \
  --format csv | gzip > open-order-ids.csv.gz
```

## Important SQL behavior

DbTransfer does not parse, parameterize, or rewrite `--sql`. Use SQL valid for the selected provider and quote it for your shell. Treat command lines and log files as sensitive if the SQL contains secrets; preferably keep secrets in the connection-string environment variable and not in SQL.

`exec` is implemented as a streaming row source and is intended for statements that return a result set. Use your provider's normal administrative tooling for scripts containing multiple statements, interactive commands, or operations whose primary purpose is modifying data.

Use [`export`](export.md) for the more task-specific query-to-file workflow, or [`inspect`](inspect.md) when you only need result column metadata.

See the generated [`exec` reference](../commands/exec.md) for every option and its default.
